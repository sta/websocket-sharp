using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using StreamThreads;
using static StreamThreads.StreamExtensions;

namespace WebSocketSharp
{
    internal class StreamThreader
    {
        public WebSocket socket;
        public NetworkStream Stream;
        public int buffersize = 1000000;

        private MemoryStream _stream = new MemoryStream();
        private StreamState state;
        private byte[] buff;

        public void Run()
        {
            buff = new byte[buffersize];
            state = MainLoop().Await();

            Stream.BeginRead(buff, 0, buffersize, StreamReader, null);

        }

        private void StreamReader(IAsyncResult at)
        {
            int len = Stream.EndRead(at);
            if (len == 0) return;

            var oldpos = _stream.Position;
            _stream.Position = _stream.Length;
            _stream.WriteBytes(buff.SubArray(0, len), len);
            _stream.Position = oldpos;

            if (!Loop()) return;

            if (_stream.Position != 0)
            {
                var oldstream = _stream;
                _stream.CopyTo(_stream = new MemoryStream(), (int)(_stream.Length - _stream.Position));
                oldstream.Dispose();
                _stream.Position = 0;
            }

            Stream.BeginRead(buff, 0, buffersize, StreamReader, null);
        }

        private bool Loop()
        {
            long curpos = _stream.Position;
            while (true)
            {
                if (_stream.Position == _stream.Length)
                    break;

                if (state.Loop()) return false;

                if (curpos == _stream.Position)
                    break;

                curpos = _stream.Position;
            }

            return true;
        }

        private IEnumerable<StreamState> MainLoop()
        {
            while (true)
            {

                yield return ReadStream(2).Await(out var header);

                var frame = WebSocketFrame.processHeader(header);

                yield return ReadExtendedPayloadLength(frame).Await();

                yield return ReadMaskingKey(frame).Await();

                yield return ReadPayloadData(frame).Await();

                if (!socket.processReceivedFrame(frame))
                    continue;

                if (!socket.HasMessage)
                    continue;

                socket._inMessage = false;

                if (socket._readyState != WebSocketState.Open)
                    break;

                socket.message();
            }

        }

        private IEnumerable<StreamState<byte[]>> ReadStream(int len)
        {
            yield return WaitFor<byte[]>(() => _stream.Length - _stream.Position >= len);

            byte[] bytes = new byte[len];
            _stream.Read(bytes, 0, len);

            yield return Return(bytes);
        }

        private IEnumerable<StreamState> ReadExtendedPayloadLength(WebSocketFrame frame)
        {
            var len = frame.ExtendedPayloadLengthWidth;
            if (len == 0)
            {
                frame._extPayloadLength = WebSocket.EmptyBytes;
                yield break;
            }

            yield return ReadStream(len).Await(out var bytes);

            if (bytes.Value.Length != len)
                throw new WebSocketException("The extended payload length of a frame cannot be read from the stream.");

            frame._extPayloadLength = bytes;
        }

        private IEnumerable<StreamState> ReadMaskingKey(WebSocketFrame frame)
        {
            var len = frame.IsMasked ? 4 : 0;
            if (len == 0)
            {
                frame._maskingKey = WebSocket.EmptyBytes;
                yield break;
            }

            yield return ReadStream(len).Await(out var bytes);

            if (bytes.Value.Length != len)
                throw new WebSocketException("The masking key of a frame cannot be read from the stream.");

            frame._maskingKey = bytes;
        }

        private IEnumerable<StreamState> ReadPayloadData(WebSocketFrame frame)
        {
            var exactLen = frame.ExactPayloadLength;

            if (exactLen > PayloadData.MaxLength)
                throw new WebSocketException(CloseStatusCode.TooBig, "A frame has too long payload length.");

            if (exactLen == 0)
            {
                frame._payloadData = PayloadData.Empty;
                yield break;
            }

            yield return ReadStream((int)exactLen).Await(out var bytes);

            if (bytes.Value.Length != (int)exactLen)
                throw new WebSocketException("The payload data of a frame cannot be read from the stream.");

            frame._payloadData = new PayloadData(bytes, (long)exactLen);
        }


    }
}
