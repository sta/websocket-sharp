// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestRadioService.cs" company="Reimers.dk">
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
//   Defines the TestRadioService type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp.Tests
{
    using System;
    using System.Threading.Tasks;

    using WebSocketSharp.Server;

    public class TestRadioService : WebSocketBehavior
    {
        protected override async Task OnMessage(MessageEventArgs e)
        {
            switch (e.Opcode)
            {
                case Opcode.Text:
                    var text = await e.Text.ReadToEndAsync().ConfigureAwait(false);
                    await Sessions.Broadcast(text).ConfigureAwait(false);
                    return;
                case Opcode.Binary:
                    await Sessions.Broadcast(e.Data).ConfigureAwait(false);
                    return;
                case Opcode.Cont:
                case Opcode.Close:
                case Opcode.Ping:
                case Opcode.Pong:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}