namespace KBP.URDT.Transport
{
    /// <summary>
    /// Thread-safe seam between the background transport (<c>DebugServer</c>) and the
    /// main-thread dispatcher: commands are submitted from the I/O thread and responses
    /// are drained for framing back. Implemented by <see cref="MainThreadDispatcher"/>.
    /// </summary>
    public interface ICommandSink
    {
        void Submit(Command command);

        bool TryDequeueResponse(out Response response);
    }
}
