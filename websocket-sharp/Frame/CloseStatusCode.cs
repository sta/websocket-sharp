#region MIT License
/**
 * CloseStatusCode.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012 sta.blockhead
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
#endregion

using System;

namespace WebSocketSharp.Frame
{
  public enum CloseStatusCode : ushort
  {
    /*
     * Close Status Code
     *
     * Defined Status Codes: http://tools.ietf.org/html/rfc6455#section-7.4.1
     *
     * "Reserved value" MUST NOT be set as a status code in a Close control frame by an endpoint.
     * It is designated for use in applications expecting a status code to indicate that connection
     * was closed due to a system grounds.
     *
     */
    NORMAL            = 1000, // Normal closure.
    AWAY              = 1001, // A Server going down or a browser having navigated away from a page.
    PROTOCOL_ERROR    = 1002, // Terminating the connection due to a protocol error.
    INCORRECT_DATA    = 1003, // Received a type of data it cannot accept.
    UNDEFINED         = 1004, // Reserved value. Still undefined.
    NO_STATUS_CODE    = 1005, // Reserved value.
    ABNORMAL          = 1006, // Reserved value. Connection was closed abnormally.
    INCONSISTENT_DATA = 1007, // Received data within a message that was not consistent with the type of the message.
    POLICY_VIOLATION  = 1008, // Received a message that violates its policy.
    TOO_BIG           = 1009, // Received a message that is too big.
    IGNORE_EXTENSION  = 1010, // Server ignored negotiated extensions.
    SERVER_ERROR      = 1011, // Server encountered an unexpected condition.
    HANDSHAKE_FAILURE = 1015  // Reserved value. Failure to establish a connection.
  }
}
