// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FragmentedMessage.cs" company="Reimers.dk">
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
//   Defines the FragmentedMessage type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp
{
	using System;
	using System.IO;
	using System.Threading;

	internal class FragmentedMessage : WebSocketMessage
	{
		private readonly Stream _stream;

	    public FragmentedMessage(Opcode opcode, Stream stream, StreamReadInfo initialRead, Func<StreamReadInfo> payloadFunc, ManualResetEventSlim waitHandle, int fragmentLength)
			: base(opcode, waitHandle, fragmentLength)
		{
			_stream = new WebSocketDataStream(stream, initialRead, payloadFunc, Consume);
			Text = new StreamReader(_stream, true);
		}

		public override Stream RawData => _stream;

	    public override StreamReader Text { get; }
	}
}