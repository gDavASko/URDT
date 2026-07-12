using System;
using System.IO;

namespace KBP.URDT.Net
{
    /// <summary>
    /// RFC 6455 frame codec — pure BCL, engine-agnostic (no UnityEngine types).
    /// Reads frames from a <see cref="Stream"/> (unmasking client frames) and encodes
    /// frames to a byte array (server frames are unmasked per spec).
    /// </summary>
    public static class WebSocketFrameCodec
    {
        private const int MAX_PAYLOAD_LENGTH = 4 * 1024 * 1024;
        private const byte FIN_BIT = 0x80;
        private const byte MASK_BIT = 0x80;
        private const byte OPCODE_MASK = 0x0F;
        private const byte LEN_MASK = 0x7F;

        /// <summary>
        /// Reads a single frame from <paramref name="stream"/>, blocking until it is
        /// complete. Returns false if the stream ends before a full frame is read.
        /// </summary>
        public static bool TryReadFrame(Stream stream, out WebSocketFrame frame)
        {
            frame = default;

            byte[] header = new byte[2];
            if (!ReadExact(stream, header, 0, 2))
            {
                return false;
            }

            bool isFinal = (header[0] & FIN_BIT) != 0;
            WebSocketOpcode opcode = (WebSocketOpcode)(header[0] & OPCODE_MASK);
            bool masked = (header[1] & MASK_BIT) != 0;
            long length = header[1] & LEN_MASK;

            if (length == 126)
            {
                byte[] ext = new byte[2];
                if (!ReadExact(stream, ext, 0, 2))
                {
                    return false;
                }

                length = (ext[0] << 8) | ext[1];
            }
            else if (length == 127)
            {
                byte[] ext = new byte[8];
                if (!ReadExact(stream, ext, 0, 8))
                {
                    return false;
                }

                length = 0;
                for (int i = 0; i < 8; i++)
                {
                    length = (length << 8) | ext[i];
                }
            }

            byte[] maskKey = null;
            if (masked)
            {
                maskKey = new byte[4];
                if (!ReadExact(stream, maskKey, 0, 4))
                {
                    return false;
                }
            }

            if (length < 0 || length > MAX_PAYLOAD_LENGTH || length > int.MaxValue)
            {
                throw new InvalidDataException("WebSocket payload exceeds the URDT limit.");
            }

            byte[] payload = new byte[(int)length];
            if (length > 0 && !ReadExact(stream, payload, 0, (int)length))
            {
                return false;
            }

            if (masked)
            {
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(payload[i] ^ maskKey[i & 3]);
                }
            }

            frame = new WebSocketFrame(opcode, payload, isFinal);
            return true;
        }

        /// <summary>
        /// Encodes a frame. Server frames are unmasked (<paramref name="maskKey"/> null);
        /// pass a 4-byte key to produce a masked (client-style) frame — used by tests.
        /// </summary>
        public static byte[] Encode(WebSocketFrame frame, byte[] maskKey = null)
        {
            byte[] payload = frame.Payload;
            int length = payload.Length;
            bool masked = maskKey != null && maskKey.Length == 4;

            using (MemoryStream ms = new MemoryStream(length + 14))
            {
                byte b0 = (byte)((frame.IsFinal ? FIN_BIT : 0) | ((byte)frame.Opcode & OPCODE_MASK));
                ms.WriteByte(b0);

                byte maskFlag = (byte)(masked ? MASK_BIT : 0);
                if (length <= 125)
                {
                    ms.WriteByte((byte)(maskFlag | (byte)length));
                }
                else if (length <= ushort.MaxValue)
                {
                    ms.WriteByte((byte)(maskFlag | 126));
                    ms.WriteByte((byte)((length >> 8) & 0xFF));
                    ms.WriteByte((byte)(length & 0xFF));
                }
                else
                {
                    ms.WriteByte((byte)(maskFlag | 127));
                    for (int i = 7; i >= 0; i--)
                    {
                        ms.WriteByte((byte)((long)((ulong)length >> (i * 8)) & 0xFF));
                    }
                }

                if (masked)
                {
                    ms.Write(maskKey, 0, 4);
                    for (int i = 0; i < length; i++)
                    {
                        ms.WriteByte((byte)(payload[i] ^ maskKey[i & 3]));
                    }
                }
                else if (length > 0)
                {
                    ms.Write(payload, 0, length);
                }

                return ms.ToArray();
            }
        }

        private static bool ReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, offset + read, count - read);
                if (n <= 0)
                {
                    return false;
                }

                read += n;
            }

            return true;
        }
    }
}
