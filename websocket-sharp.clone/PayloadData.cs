/*
 * PayloadData.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2014 sta.blockhead
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

namespace WebSocketSharp
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    internal class PayloadData : IEnumerable<byte>
    {
        private readonly byte[] _data;
        private readonly long _length;
        private bool _masked;
        
        internal PayloadData()
        {
            _data = new byte[0];
        }
        
        internal PayloadData(byte[] data)
        {
            _data = data;
            _masked = false;
            _length = data.LongLength;
        }

        public byte[] ApplicationData => _data;

        public ulong Length => (ulong)_length;

        internal void Mask(byte[] key)
        {
            for (long i = 0; i < _length; i++)
            {
                _data[i] = (byte)(_data[i] ^ key[i % 4]);
            }

            _masked = !_masked;
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return ((IEnumerable<byte>)_data).GetEnumerator();
        }

        public byte[] ToByteArray()
        {
            return _data;
        }

        public override string ToString()
        {
            return BitConverter.ToString(_data);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
