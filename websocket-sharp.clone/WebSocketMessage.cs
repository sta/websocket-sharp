// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebSocketMessage.cs" company="Reimers.dk">
//   The MIT License
//   Copyright (c) 2012-2014 sta.blockhead
//   Copyright (c) 2014 Reimers.dk
//   
//   Permission is hereby granted, free of charge, to any person obtaining a copy  of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//   
//   The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//   
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary>
//   Defines the WebSocketMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class WebSocketMessage
    {
        private readonly ManualResetEventSlim _waitHandle;

        private readonly int _fragmentLength;

        protected WebSocketMessage(Opcode opcode, ManualResetEventSlim waitHandle, int fragmentLength)
        {
            _waitHandle = waitHandle;
            _fragmentLength = fragmentLength;
            Opcode = opcode;
        }

        public Opcode Opcode { get; private set; }

        public abstract Stream RawData { get; }

        public abstract StreamReader Text { get; }

        internal async Task Consume()
        {
            if (RawData != null)
            {
                var buffer = new byte[_fragmentLength];
                while (await RawData.ReadAsync(buffer, 0, _fragmentLength).ConfigureAwait(false) == _fragmentLength)
                {
                }
            }

            _waitHandle.Set();
        }
    }
}