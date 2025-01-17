#region License
/*
 * WebSocketFrame.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2025 sta.blockhead
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

#region Contributors
/*
 * Contributors:
 * - Chris Swiedler
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebSocketSharp
{
  internal class WebSocketFrame : IEnumerable<byte>
  {
    #region Private Fields

    private static readonly int    _defaultHeaderLength;
    private static readonly int    _defaultMaskingKeyLength;
    private static readonly byte[] _emptyBytes;
    private byte[]                 _extPayloadLength;
    private Fin                    _fin;
    private Mask                   _mask;
    private byte[]                 _maskingKey;
    private Opcode                 _opcode;
    private PayloadData            _payloadData;
    private int                    _payloadLength;
    private Rsv                    _rsv1;
    private Rsv                    _rsv2;
    private Rsv                    _rsv3;

    #endregion

    #region Static Constructor

    static WebSocketFrame ()
    {
      _defaultHeaderLength = 2;
      _defaultMaskingKeyLength = 4;
      _emptyBytes = new byte[0];
    }

    #endregion

    #region Private Constructors

    private WebSocketFrame ()
    {
    }

    #endregion

    #region Internal Constructors

    internal WebSocketFrame (
      Fin fin,
      Opcode opcode,
      byte[] data,
      bool compressed,
      bool mask
    )
      : this (fin, opcode, new PayloadData (data), compressed, mask)
    {
    }

    internal WebSocketFrame (
      Fin fin,
      Opcode opcode,
      PayloadData payloadData,
      bool compressed,
      bool mask
    )
    {
      _fin = fin;
      _opcode = opcode;

      _rsv1 = compressed ? Rsv.On : Rsv.Off;
      _rsv2 = Rsv.Off;
      _rsv3 = Rsv.Off;

      var len = payloadData.Length;

      if (len < 126) {
        _payloadLength = (int) len;
        _extPayloadLength = _emptyBytes;
      }
      else if (len < 0x010000) {
        _payloadLength = 126;
        _extPayloadLength = ((ushort) len).ToByteArray (ByteOrder.Big);
      }
      else {
        _payloadLength = 127;
        _extPayloadLength = len.ToByteArray (ByteOrder.Big);
      }

      if (mask) {
        _mask = Mask.On;
        _maskingKey = createMaskingKey ();

        payloadData.Mask (_maskingKey);
      }
      else {
        _mask = Mask.Off;
        _maskingKey = _emptyBytes;
      }

      _payloadData = payloadData;
    }

    #endregion

    #region Internal Properties

    internal ulong ExactPayloadLength {
      get {
        return _payloadLength < 126
               ? (ulong) _payloadLength
               : _payloadLength == 126
                 ? _extPayloadLength.ToUInt16 (ByteOrder.Big)
                 : _extPayloadLength.ToUInt64 (ByteOrder.Big);
      }
    }

    internal int ExtendedPayloadLengthWidth {
      get {
        return _payloadLength < 126
               ? 0
               : _payloadLength == 126
                 ? 2
                 : 8;
      }
    }

    #endregion

    #region Public Properties

    public byte[] ExtendedPayloadLength {
      get {
        return _extPayloadLength;
      }
    }

    public Fin Fin {
      get {
        return _fin;
      }
    }

    public bool IsBinary {
      get {
        return _opcode == Opcode.Binary;
      }
    }

    public bool IsClose {
      get {
        return _opcode == Opcode.Close;
      }
    }

    public bool IsCompressed {
      get {
        return _rsv1 == Rsv.On;
      }
    }

    public bool IsContinuation {
      get {
        return _opcode == Opcode.Cont;
      }
    }

    public bool IsControl {
      get {
        return _opcode >= Opcode.Close;
      }
    }

    public bool IsData {
      get {
        return _opcode == Opcode.Text || _opcode == Opcode.Binary;
      }
    }

    public bool IsFinal {
      get {
        return _fin == Fin.Final;
      }
    }

    public bool IsFragment {
      get {
        return _fin == Fin.More || _opcode == Opcode.Cont;
      }
    }

    public bool IsMasked {
      get {
        return _mask == Mask.On;
      }
    }

    public bool IsPing {
      get {
        return _opcode == Opcode.Ping;
      }
    }

    public bool IsPong {
      get {
        return _opcode == Opcode.Pong;
      }
    }

    public bool IsText {
      get {
        return _opcode == Opcode.Text;
      }
    }

    public ulong Length {
      get {
        return (ulong) (
                 _defaultHeaderLength
                 + _extPayloadLength.Length
                 + _maskingKey.Length
               )
               + _payloadData.Length;
      }
    }

    public Mask Mask {
      get {
        return _mask;
      }
    }

    public byte[] MaskingKey {
      get {
        return _maskingKey;
      }
    }

    public Opcode Opcode {
      get {
        return _opcode;
      }
    }

    public PayloadData PayloadData {
      get {
        return _payloadData;
      }
    }

    public int PayloadLength {
      get {
        return _payloadLength;
      }
    }

    public Rsv Rsv1 {
      get {
        return _rsv1;
      }
    }

    public Rsv Rsv2 {
      get {
        return _rsv2;
      }
    }

    public Rsv Rsv3 {
      get {
        return _rsv3;
      }
    }

    #endregion

    #region Private Methods

    private static byte[] createMaskingKey ()
    {
      var key = new byte[_defaultMaskingKeyLength];

      WebSocket.RandomNumber.GetBytes (key);

      return key;
    }

    private static WebSocketFrame processHeader (byte[] header)
    {
      if (header.Length != _defaultHeaderLength) {
        var msg = "The header part of a frame could not be read.";

        throw new WebSocketException (msg);
      }

      // FIN
      var fin = (header[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;

      // RSV1
      var rsv1 = (header[0] & 0x40) == 0x40 ? Rsv.On : Rsv.Off;

      // RSV2
      var rsv2 = (header[0] & 0x20) == 0x20 ? Rsv.On : Rsv.Off;

      // RSV3
      var rsv3 = (header[0] & 0x10) == 0x10 ? Rsv.On : Rsv.Off;

      // Opcode
      var opcode = header[0] & 0x0f;

      // MASK
      var mask = (header[1] & 0x80) == 0x80 ? Mask.On : Mask.Off;

      // Payload Length
      var payloadLen = header[1] & 0x7f;

      if (!opcode.IsSupportedOpcode ()) {
        var msg = "The opcode of a frame is not supported.";

        throw new WebSocketException (CloseStatusCode.UnsupportedData, msg);
      }

      var frame = new WebSocketFrame ();

      frame._fin = fin;
      frame._rsv1 = rsv1;
      frame._rsv2 = rsv2;
      frame._rsv3 = rsv3;
      frame._opcode = (Opcode) opcode;
      frame._mask = mask;
      frame._payloadLength = payloadLen;

      return frame;
    }

    private static WebSocketFrame readExtendedPayloadLength (
      Stream stream,
      WebSocketFrame frame
    )
    {
      var len = frame.ExtendedPayloadLengthWidth;

      if (len == 0) {
        frame._extPayloadLength = _emptyBytes;

        return frame;
      }

      var bytes = stream.ReadBytes (len);

      if (bytes.Length != len) {
        var msg = "The extended payload length of a frame could not be read.";

        throw new WebSocketException (msg);
      }

      frame._extPayloadLength = bytes;

      return frame;
    }

    private static void readExtendedPayloadLengthAsync (
      Stream stream,
      WebSocketFrame frame,
      Action<WebSocketFrame> completed,
      Action<Exception> error
    )
    {
      var len = frame.ExtendedPayloadLengthWidth;

      if (len == 0) {
        frame._extPayloadLength = _emptyBytes;

        completed (frame);

        return;
      }

      stream.ReadBytesAsync (
        len,
        bytes => {
          if (bytes.Length != len) {
            var msg = "The extended payload length of a frame could not be read.";

            throw new WebSocketException (msg);
          }

          frame._extPayloadLength = bytes;

          completed (frame);
        },
        error
      );
    }

    private static WebSocketFrame readHeader (Stream stream)
    {
      var bytes = stream.ReadBytes (_defaultHeaderLength);

      return processHeader (bytes);
    }

    private static void readHeaderAsync (
      Stream stream,
      Action<WebSocketFrame> completed,
      Action<Exception> error
    )
    {
      stream.ReadBytesAsync (
        _defaultHeaderLength,
        bytes => {
          var frame = processHeader (bytes);

          completed (frame);
        },
        error
      );
    }

    private static WebSocketFrame readMaskingKey (
      Stream stream,
      WebSocketFrame frame
    )
    {
      if (!frame.IsMasked) {
        frame._maskingKey = _emptyBytes;

        return frame;
      }

      var bytes = stream.ReadBytes (_defaultMaskingKeyLength);

      if (bytes.Length != _defaultMaskingKeyLength) {
        var msg = "The masking key of a frame could not be read.";

        throw new WebSocketException (msg);
      }

      frame._maskingKey = bytes;

      return frame;
    }

    private static void readMaskingKeyAsync (
      Stream stream,
      WebSocketFrame frame,
      Action<WebSocketFrame> completed,
      Action<Exception> error
    )
    {
      if (!frame.IsMasked) {
        frame._maskingKey = _emptyBytes;

        completed (frame);

        return;
      }

      stream.ReadBytesAsync (
        _defaultMaskingKeyLength,
        bytes => {
          if (bytes.Length != _defaultMaskingKeyLength) {
            var msg = "The masking key of a frame could not be read.";

            throw new WebSocketException (msg);
          }

          frame._maskingKey = bytes;

          completed (frame);
        },
        error
      );
    }

    private static WebSocketFrame readPayloadData (
      Stream stream,
      WebSocketFrame frame
    )
    {
      var exactPayloadLen = frame.ExactPayloadLength;

      if (exactPayloadLen > PayloadData.MaxLength) {
        var msg = "The payload data of a frame is too big.";

        throw new WebSocketException (CloseStatusCode.TooBig, msg);
      }

      if (exactPayloadLen == 0) {
        frame._payloadData = PayloadData.Empty;

        return frame;
      }

      var len = (long) exactPayloadLen;
      var bytes = frame._payloadLength > 126
                  ? stream.ReadBytes (len, 1024)
                  : stream.ReadBytes ((int) len);

      if (bytes.LongLength != len) {
        var msg = "The payload data of a frame could not be read.";

        throw new WebSocketException (msg);
      }

      frame._payloadData = new PayloadData (bytes, len);

      return frame;
    }

    private static void readPayloadDataAsync (
      Stream stream,
      WebSocketFrame frame,
      Action<WebSocketFrame> completed,
      Action<Exception> error
    )
    {
      var exactPayloadLen = frame.ExactPayloadLength;

      if (exactPayloadLen > PayloadData.MaxLength) {
        var msg = "The payload data of a frame is too big.";

        throw new WebSocketException (CloseStatusCode.TooBig, msg);
      }

      if (exactPayloadLen == 0) {
        frame._payloadData = PayloadData.Empty;

        completed (frame);

        return;
      }

      var len = (long) exactPayloadLen;

      Action<byte[]> comp =
        bytes => {
          if (bytes.LongLength != len) {
            var msg = "The payload data of a frame could not be read.";

            throw new WebSocketException (msg);
          }

          frame._payloadData = new PayloadData (bytes, len);

          completed (frame);
        };

      if (frame._payloadLength > 126) {
        stream.ReadBytesAsync (len, 1024, comp, error);

        return;
      }

      stream.ReadBytesAsync ((int) len, comp, error);
    }

    private string toDumpString ()
    {
      var len = Length;
      var cnt = (long) (len / 4);
      var rem = (int) (len % 4);

      string spFmt;
      string cntFmt;

      if (cnt < 10000) {
        spFmt = "{0,4}";
        cntFmt = "{0,4}";
      }
      else if (cnt < 0x010000) {
        spFmt = "{0,4}";
        cntFmt = "{0,4:X}";
      }
      else if (cnt < 0x0100000000) {
        spFmt = "{0,8}";
        cntFmt = "{0,8:X}";
      }
      else {
        spFmt = "{0,16}";
        cntFmt = "{0,16:X}";
      }

      var baseFmt = @"{0} 01234567 89ABCDEF 01234567 89ABCDEF
{0}+--------+--------+--------+--------+
";
      var headerFmt = String.Format (baseFmt, spFmt);

      baseFmt = "{0}|{{1,8}} {{2,8}} {{3,8}} {{4,8}}|\n";
      var lineFmt = String.Format (baseFmt, cntFmt);

      baseFmt = "{0}+--------+--------+--------+--------+";
      var footerFmt = String.Format (baseFmt, spFmt);

      var buff = new StringBuilder (64);

      Func<Action<string, string, string, string>> lineWriter =
        () => {
          long lineCnt = 0;

          return (arg1, arg2, arg3, arg4) => {
                   buff.AppendFormat (
                     lineFmt,
                     ++lineCnt,
                     arg1,
                     arg2,
                     arg3,
                     arg4
                   );
                 };
        };

      var writeLine = lineWriter ();
      var bytes = ToArray ();

      buff.AppendFormat (headerFmt, String.Empty);

      for (long i = 0; i <= cnt; i++) {
        var j = i * 4;

        if (i < cnt) {
          var arg1 = Convert.ToString (bytes[j], 2).PadLeft (8, '0');
          var arg2 = Convert.ToString (bytes[j + 1], 2).PadLeft (8, '0');
          var arg3 = Convert.ToString (bytes[j + 2], 2).PadLeft (8, '0');
          var arg4 = Convert.ToString (bytes[j + 3], 2).PadLeft (8, '0');

          writeLine (arg1, arg2, arg3, arg4);

          continue;
        }

        if (rem > 0) {
          var arg1 = Convert.ToString (bytes[j], 2).PadLeft (8, '0');
          var arg2 = rem >= 2
                     ? Convert.ToString (bytes[j + 1], 2).PadLeft (8, '0')
                     : String.Empty;

          var arg3 = rem == 3
                     ? Convert.ToString (bytes[j + 2], 2).PadLeft (8, '0')
                     : String.Empty;

          writeLine (arg1, arg2, arg3, String.Empty);
        }
      }

      buff.AppendFormat (footerFmt, String.Empty);

      return buff.ToString ();
    }

    private string toString ()
    {
      var extPayloadLen = _payloadLength >= 126
                          ? ExactPayloadLength.ToString ()
                          : String.Empty;

      var maskingKey = _mask == Mask.On
                       ? BitConverter.ToString (_maskingKey)
                       : String.Empty;

      var payloadData = _payloadLength >= 126
                        ? "***"
                        : _payloadLength > 0
                          ? _payloadData.ToString ()
                          : String.Empty;

      var fmt = @"                    FIN: {0}
                   RSV1: {1}
                   RSV2: {2}
                   RSV3: {3}
                 Opcode: {4}
                   MASK: {5}
         Payload Length: {6}
Extended Payload Length: {7}
            Masking Key: {8}
           Payload Data: {9}";

      return String.Format (
               fmt,
               _fin,
               _rsv1,
               _rsv2,
               _rsv3,
               _opcode,
               _mask,
               _payloadLength,
               extPayloadLen,
               maskingKey,
               payloadData
             );
    }

    #endregion

    #region Internal Methods

    internal static WebSocketFrame CreateCloseFrame (
      PayloadData payloadData,
      bool mask
    )
    {
      return new WebSocketFrame (
               Fin.Final,
               Opcode.Close,
               payloadData,
               false,
               mask
             );
    }

    internal static WebSocketFrame CreatePingFrame (bool mask)
    {
      return new WebSocketFrame (
               Fin.Final,
               Opcode.Ping,
               PayloadData.Empty,
               false,
               mask
             );
    }

    internal static WebSocketFrame CreatePingFrame (byte[] data, bool mask)
    {
      return new WebSocketFrame (
               Fin.Final,
               Opcode.Ping,
               new PayloadData (data),
               false,
               mask
             );
    }

    internal static WebSocketFrame CreatePongFrame (
      PayloadData payloadData,
      bool mask
    )
    {
      return new WebSocketFrame (
               Fin.Final,
               Opcode.Pong,
               payloadData,
               false,
               mask
             );
    }

    internal static WebSocketFrame ReadFrame (Stream stream, bool unmask)
    {
      var frame = readHeader (stream);

      readExtendedPayloadLength (stream, frame);
      readMaskingKey (stream, frame);
      readPayloadData (stream, frame);

      if (unmask)
        frame.Unmask ();

      return frame;
    }

    internal static void ReadFrameAsync (
      Stream stream,
      bool unmask,
      Action<WebSocketFrame> completed,
      Action<Exception> error
    )
    {
      readHeaderAsync (
        stream,
        frame =>
          readExtendedPayloadLengthAsync (
            stream,
            frame,
            frame1 =>
              readMaskingKeyAsync (
                stream,
                frame1,
                frame2 =>
                  readPayloadDataAsync (
                    stream,
                    frame2,
                    frame3 => {
                      if (unmask)
                        frame3.Unmask ();

                      completed (frame3);
                    },
                    error
                  ),
                error
              ),
            error
          ),
        error
      );
    }

    internal string ToString (bool dump)
    {
      return dump ? toDumpString () : toString ();
    }

    internal void Unmask ()
    {
      if (_mask == Mask.Off)
        return;

      _payloadData.Mask (_maskingKey);

      _maskingKey = _emptyBytes;
      _mask = Mask.Off;
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator ()
    {
      foreach (var b in ToArray ())
        yield return b;
    }

    public byte[] ToArray ()
    {
      using (var buff = new MemoryStream ()) {
        var header = (int) _fin;

        header = (header << 1) + (int) _rsv1;
        header = (header << 1) + (int) _rsv2;
        header = (header << 1) + (int) _rsv3;
        header = (header << 4) + (int) _opcode;
        header = (header << 1) + (int) _mask;
        header = (header << 7) + _payloadLength;

        var headerAsUInt16 = (ushort) header;
        var headerAsBytes = headerAsUInt16.ToByteArray (ByteOrder.Big);

        buff.Write (headerAsBytes, 0, _defaultHeaderLength);

        if (_payloadLength >= 126)
          buff.Write (_extPayloadLength, 0, _extPayloadLength.Length);

        if (_mask == Mask.On)
          buff.Write (_maskingKey, 0, _defaultMaskingKeyLength);

        if (_payloadLength > 0) {
          var bytes = _payloadData.ToArray ();

          if (_payloadLength > 126)
            buff.WriteBytes (bytes, 1024);
          else
            buff.Write (bytes, 0, bytes.Length);
        }

        buff.Close ();

        return buff.ToArray ();
      }
    }

    public override string ToString ()
    {
      var val = ToArray ();

      return BitConverter.ToString (val);
    }

    #endregion

    #region Explicit Interface Implementations

    IEnumerator IEnumerable.GetEnumerator ()
    {
      return GetEnumerator ();
    }

    #endregion
  }
}
