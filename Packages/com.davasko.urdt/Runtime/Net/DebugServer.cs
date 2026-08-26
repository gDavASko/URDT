using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KBP.URDT.Net
{
    /// <summary>
    /// Loopback WebSocket server with connection replacement, bounded reads and
    /// connection-scoped messaging. A newly accepted client supersedes a stale client,
    /// so one half-open socket cannot block subsequent automation sessions.
    /// </summary>
    public sealed class DebugServer : IDisposable
    {
        private const int DEFAULT_RECEIVE_TIMEOUT_MS = 30000;
        private const int DEFAULT_SEND_TIMEOUT_MS = 5000;
        private const int ACCEPT_THREAD_JOIN_MS = 1000;
        private const int CONNECTION_THREAD_JOIN_MS = 250;

        private readonly object _connectionsLock = new object();
        private readonly Dictionary<long, ClientConnection> _connections =
            new Dictionary<long, ClientConnection>();

        private TcpListener _listener;
        private Thread _acceptThread;
        private ClientConnection _activeConnection;
        private long _nextConnectionId;
        private volatile bool _running;

        /// <summary>Legacy request processor used by transport-only tests.</summary>
        public Func<string, string> MessageProcessor { get; set; }

        /// <summary>Legacy text handler. Prefer <see cref="ConnectionMessageHandler"/>.</summary>
        public Action<string> MessageHandler { get; set; }

        /// <summary>Receives text together with the connection generation.</summary>
        public Action<UrdtConnectionMessage> ConnectionMessageHandler { get; set; }

        /// <summary>Optional transport log sink. It may be called from background threads.</summary>
        public Action<string> OnLog { get; set; }

        /// <summary>Legacy notification raised after the WebSocket upgrade.</summary>
        public Action OnClientConnected { get; set; }

        /// <summary>Raised after a connection completes the WebSocket upgrade.</summary>
        public Action<long> ClientConnected { get; set; }

        /// <summary>Raised when a connection is released.</summary>
        public Action<long> ClientDisconnected { get; set; }

        /// <summary>Gets whether the listener is accepting clients.</summary>
        public bool IsRunning
        {
            get { return _running; }
        }

        /// <summary>Gets the actual bound port.</summary>
        public int Port { get; private set; }

        /// <summary>Gets whether an upgraded client is currently active.</summary>
        public bool HasClient
        {
            get
            {
                lock (_connectionsLock)
                {
                    return _activeConnection != null && !_activeConnection.IsClosed;
                }
            }
        }

        /// <summary>Gets the active connection generation, or zero when disconnected.</summary>
        public long ActiveConnectionId
        {
            get
            {
                lock (_connectionsLock)
                {
                    return _activeConnection != null ? _activeConnection.Id : 0L;
                }
            }
        }

        /// <summary>
        /// Starts on the requested port. Use port zero to request an ephemeral port.
        /// </summary>
        public void Start(int port, IPAddress bindAddress = null)
        {
            StartWithFallback(port, 0, bindAddress);
        }

        /// <summary>
        /// Starts on the preferred port, scans subsequent ports on collision, then falls
        /// back to an ephemeral port. Returns the actual bound port.
        /// </summary>
        public int StartWithFallback(int preferredPort, int portSearchCount, IPAddress bindAddress = null)
        {
            if (_running)
            {
                return Port;
            }

            IPAddress address = bindAddress ?? IPAddress.Loopback;
            TcpListener listener = BindListener(address, preferredPort, portSearchCount);

            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _running = true;
            _acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "URDT_Accept"
            };
            _acceptThread.Start();
            return Port;
        }

        /// <summary>Stops the listener and every known connection.</summary>
        public void Stop()
        {
            if (!_running && _listener == null)
            {
                return;
            }

            _running = false;

            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
            catch (Exception exception)
            {
                Log("listener stop: " + exception.Message);
            }

            ClientConnection[] connections;
            lock (_connectionsLock)
            {
                connections = new ClientConnection[_connections.Count];
                _connections.Values.CopyTo(connections, 0);
                _connections.Clear();
                _activeConnection = null;
            }

            for (int i = 0; i < connections.Length; i++)
            {
                connections[i].Close();
            }

            JoinThread(_acceptThread, ACCEPT_THREAD_JOIN_MS);
            for (int i = 0; i < connections.Length; i++)
            {
                JoinThread(connections[i].Thread, CONNECTION_THREAD_JOIN_MS);
            }

            _acceptThread = null;
            _listener = null;
            Port = 0;
        }

        /// <summary>Sends text to the active client.</summary>
        public bool Send(string text)
        {
            return Send(ActiveConnectionId, text);
        }

        /// <summary>Sends text only when the specified connection is still active.</summary>
        public bool Send(long connectionId, string text)
        {
            ClientConnection connection = GetActiveConnection(connectionId);
            if (connection == null || connection.Stream == null)
            {
                return false;
            }

            byte[] bytes = WebSocketFrameCodec.Encode(WebSocketFrame.Text(text));
            lock (connection.SendLock)
            {
                try
                {
                    connection.Stream.Write(bytes, 0, bytes.Length);
                    return true;
                }
                catch (Exception exception)
                {
                    Log("send[" + connectionId + "]: " + exception.Message);
                    connection.Close();
                    return false;
                }
            }
        }

        /// <summary>Closes the active connection while keeping the listener alive.</summary>
        public void Close()
        {
            Close(ActiveConnectionId);
        }

        /// <summary>Closes one connection generation.</summary>
        public void Close(long connectionId)
        {
            ClientConnection connection = GetConnection(connectionId);
            if (connection == null)
            {
                return;
            }

            SendControl(connection, WebSocketOpcode.Close, Array.Empty<byte>());
            connection.Close();
        }

        /// <summary>Stops and disposes the server.</summary>
        public void Dispose()
        {
            Stop();
        }

        private static TcpListener BindListener(IPAddress address, int preferredPort, int portSearchCount)
        {
            if (preferredPort <= 0)
            {
                return StartListener(address, 0);
            }

            int attempts = Math.Max(0, portSearchCount);
            SocketException lastCollision = null;
            for (int offset = 0; offset <= attempts; offset++)
            {
                try
                {
                    return StartListener(address, preferredPort + offset);
                }
                catch (SocketException exception)
                {
                    lastCollision = exception;
                }
            }

            try
            {
                return StartListener(address, 0);
            }
            catch (SocketException)
            {
                throw lastCollision ?? new SocketException((int)SocketError.AddressAlreadyInUse);
            }
        }

        private static TcpListener StartListener(IPAddress address, int port)
        {
            TcpListener listener = new TcpListener(address, port);
            listener.Start();
            return listener;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception exception)
                {
                    if (!_running)
                    {
                        return;
                    }

                    Log("accept: " + exception.Message);
                    continue;
                }

                ConfigureClient(client);
                ClientConnection connection = new ClientConnection(
                    Interlocked.Increment(ref _nextConnectionId), client);
                ReplaceActiveConnection(connection);

                Thread thread = new Thread(() => HandleClient(connection))
                {
                    IsBackground = true,
                    Name = "URDT_Client_" + connection.Id
                };
                connection.Thread = thread;
                thread.Start();
            }
        }

        private static void ConfigureClient(TcpClient client)
        {
            client.NoDelay = true;
            client.ReceiveTimeout = DEFAULT_RECEIVE_TIMEOUT_MS;
            client.SendTimeout = DEFAULT_SEND_TIMEOUT_MS;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }

        private void ReplaceActiveConnection(ClientConnection connection)
        {
            ClientConnection previous;
            lock (_connectionsLock)
            {
                previous = _activeConnection;
                _activeConnection = connection;
                _connections[connection.Id] = connection;
            }

            if (previous != null)
            {
                Log("connection " + previous.Id + " superseded by " + connection.Id);
                previous.Close();
            }
        }

        private void HandleClient(ClientConnection connection)
        {
            bool upgraded = false;
            try
            {
                connection.Stream = connection.Client.GetStream();
                connection.Stream.ReadTimeout = DEFAULT_RECEIVE_TIMEOUT_MS;
                connection.Stream.WriteTimeout = DEFAULT_SEND_TIMEOUT_MS;

                if (!PerformHandshake(connection.Stream))
                {
                    return;
                }

                if (!IsActive(connection.Id))
                {
                    return;
                }

                upgraded = true;
                InvokeConnected(connection.Id);
                ReadLoop(connection);
            }
            catch (IOException exception)
            {
                Log("client[" + connection.Id + "] I/O: " + exception.Message);
            }
            catch (Exception exception)
            {
                Log("client[" + connection.Id + "]: " + exception.Message);
            }
            finally
            {
                ReleaseConnection(connection);
                if (upgraded)
                {
                    InvokeDisconnected(connection.Id);
                }
            }
        }

        private static bool PerformHandshake(NetworkStream stream)
        {
            string request = ReadHttpRequest(stream);
            Dictionary<string, string> headers = WebSocketHandshake.ParseHeaders(request);

            if (!WebSocketHandshake.IsWebSocketUpgrade(headers))
            {
                byte[] reject = WebSocketHandshake.BuildRejectResponse(400, "Bad Request");
                stream.Write(reject, 0, reject.Length);
                return false;
            }

            byte[] accept = WebSocketHandshake.BuildAcceptResponse(headers["Sec-WebSocket-Key"]);
            stream.Write(accept, 0, accept.Length);
            return true;
        }

        private void ReadLoop(ClientConnection connection)
        {
            while (_running && IsActive(connection.Id) && !connection.IsClosed)
            {
                WebSocketFrame frame;
                if (!WebSocketFrameCodec.TryReadFrame(connection.Stream, out frame))
                {
                    return;
                }

                switch (frame.Opcode)
                {
                    case WebSocketOpcode.Text:
                        DispatchText(connection.Id, frame.GetText());
                        break;

                    case WebSocketOpcode.Ping:
                        SendControl(connection, WebSocketOpcode.Pong, frame.Payload);
                        break;

                    case WebSocketOpcode.Close:
                        SendControl(connection, WebSocketOpcode.Close, Array.Empty<byte>());
                        return;

                    default:
                        break;
                }
            }
        }

        private void DispatchText(long connectionId, string message)
        {
            Action<UrdtConnectionMessage> connectionHandler = ConnectionMessageHandler;
            if (connectionHandler != null)
            {
                connectionHandler(new UrdtConnectionMessage(connectionId, message));
                return;
            }

            Action<string> handler = MessageHandler;
            if (handler != null)
            {
                handler(message);
                return;
            }

            Func<string, string> processor = MessageProcessor;
            if (processor == null)
            {
                return;
            }

            string response = processor(message);
            if (!string.IsNullOrEmpty(response))
            {
                Send(connectionId, response);
            }
        }

        private void SendControl(ClientConnection connection, WebSocketOpcode opcode, byte[] payload)
        {
            if (connection == null || connection.Stream == null || connection.IsClosed)
            {
                return;
            }

            byte[] bytes = WebSocketFrameCodec.Encode(new WebSocketFrame(opcode, payload, true));
            lock (connection.SendLock)
            {
                try
                {
                    connection.Stream.Write(bytes, 0, bytes.Length);
                }
                catch (Exception exception)
                {
                    Log("control[" + connection.Id + "]: " + exception.Message);
                }
            }
        }

        private static string ReadHttpRequest(NetworkStream stream)
        {
            StringBuilder builder = new StringBuilder();
            byte[] one = new byte[1];
            int matched = 0;

            while (matched < 4)
            {
                int read = stream.Read(one, 0, 1);
                if (read <= 0)
                {
                    break;
                }

                char c = (char)one[0];
                builder.Append(c);

                if ((matched == 0 || matched == 2) && c == '\r')
                {
                    matched++;
                }
                else if ((matched == 1 || matched == 3) && c == '\n')
                {
                    matched++;
                }
                else
                {
                    matched = 0;
                }

                if (builder.Length > 8192)
                {
                    throw new InvalidDataException("WebSocket upgrade headers exceed 8 KB.");
                }
            }

            return builder.ToString();
        }

        private ClientConnection GetActiveConnection(long connectionId)
        {
            lock (_connectionsLock)
            {
                return _activeConnection != null
                    && _activeConnection.Id == connectionId
                    && !_activeConnection.IsClosed
                    ? _activeConnection
                    : null;
            }
        }

        private ClientConnection GetConnection(long connectionId)
        {
            lock (_connectionsLock)
            {
                ClientConnection connection;
                return _connections.TryGetValue(connectionId, out connection) ? connection : null;
            }
        }

        private bool IsActive(long connectionId)
        {
            return GetActiveConnection(connectionId) != null;
        }

        private void ReleaseConnection(ClientConnection connection)
        {
            connection.Close();
            lock (_connectionsLock)
            {
                _connections.Remove(connection.Id);
                if (_activeConnection == connection)
                {
                    _activeConnection = null;
                }
            }
        }

        private void InvokeConnected(long connectionId)
        {
            try
            {
                if (OnClientConnected != null)
                {
                    OnClientConnected();
                }

                if (ClientConnected != null)
                {
                    ClientConnected(connectionId);
                }
            }
            catch (Exception exception)
            {
                Log("connected callback: " + exception.Message);
            }
        }

        private void InvokeDisconnected(long connectionId)
        {
            try
            {
                if (ClientDisconnected != null)
                {
                    ClientDisconnected(connectionId);
                }
            }
            catch (Exception exception)
            {
                Log("disconnected callback: " + exception.Message);
            }
        }

        private static void JoinThread(Thread thread, int timeoutMs)
        {
            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
            {
                thread.Join(timeoutMs);
            }
        }

        private void Log(string message)
        {
            Action<string> sink = OnLog;
            if (sink != null)
            {
                sink(message);
            }
        }

        private sealed class ClientConnection
        {
            private int _closed;

            public ClientConnection(long id, TcpClient client)
            {
                Id = id;
                Client = client;
            }

            public readonly object SendLock = new object();

            public long Id { get; }

            public TcpClient Client { get; }

            public NetworkStream Stream { get; set; }

            public Thread Thread { get; set; }

            public bool IsClosed
            {
                get { return Volatile.Read(ref _closed) != 0; }
            }

            public void Close()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0)
                {
                    return;
                }

                try
                {
                    if (Stream != null)
                    {
                        Stream.Close();
                    }
                }
                catch (Exception)
                {
                }

                try
                {
                    Client.Close();
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
