#region License
/*
 * WebSocketFrame.cs
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

    private byte[]      _extPayloadLength;
    private Fin         _fin;
    private Mask        _mask;
    private byte[]      _maskingKey;
    private Opcode      _opcode;
    private PayloadData _payloadData;
    private byte        _payloadLength;
    private Rsv         _rsv1;
    private Rsv         _rsv2;
    private Rsv         _rsv3;

    #endregion

    #region Internal Fields

    internal static readonly byte[] EmptyUnmaskPingBytes;

    #endregion

    #region Static Constructor

    static WebSocketFrame ()
    {
      EmptyUnmaskPingBytes = CreatePingFrame (false).ToByteArray ();
    }

    #endregion

    #region Private Constructors

    private WebSocketFrame ()
    {
    }

    #endregion

    #region Internal Constructors

    internal WebSocketFrame (Opcode opcode, PayloadData payloadData, bool mask)
      : this (Fin.Final, opcode, payloadData, false, mask)
    {
    }

    internal WebSocketFrame (Fin fin, Opcode opcode, byte[] data, bool compressed, bool mask)
      : this (fin, opcode, new PayloadData (data), compressed, mask)
    {
    }

    internal WebSocketFrame (
      Fin fin, Opcode opcode, PayloadData payloadData, bool compressed, bool mask)
    {
      _fin = fin;
      _rsv1 = isData (opcode) && compressed ? Rsv.On : Rsv.Off;
      _rsv2 = Rsv.Off;
      _rsv3 = Rsv.Off;
      _opcode = opcode;

      var len = payloadData.Length;
      if (len < 126) {
        _payloadLength = (byte) len;
        _extPayloadLength = new byte[0];
      }
      else if (len < 0x010000) {
        _payloadLength = (byte) 126;
        _extPayloadLength = ((ushort) len).InternalToByteArray (ByteOrder.Big);
      }
      else {
        _payloadLength = (byte) 127;
        _extPayloadLength = len.InternalToByteArray (ByteOrder.Big);
      }

      if (mask) {
        _mask = Mask.Mask;
        _maskingKey = createMaskingKey ();
        payloadData.Mask (_maskingKey);
      }
      else {
        _mask = Mask.Unmask;
        _maskingKey = new byte[0];
      }

      _payloadData = payloadData;
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
        return _opcode == Opcode.Close || _opcode == Opcode.Ping || _opcode == Opcode.Pong;
      }
    }

    public bool IsData {
      get {
        return _opcode == Opcode.Binary || _opcode == Opcode.Text;
      }
    }

    public bool IsFinal {
      get {
        return _fin == Fin.Final;
      }
    }

    public bool IsFragmented {
      get {
        return _fin == Fin.More || _opcode == Opcode.Cont;
      }
    }

    public bool IsMasked {
      get {
        return _mask == Mask.Mask;
      }
    }

    public bool IsPerMessageCompressed {
      get {
        return (_opcode == Opcode.Binary || _opcode == Opcode.Text) && _rsv1 == Rsv.On;
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
        return 2 + (ulong) (_extPayloadLength.Length + _maskingKey.Length) + _payloadData.Length;
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

    public byte PayloadLength {
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
      var key = new byte[4];
      var rand = new Random ();
      rand.NextBytes (key);

      return key;
    }

    private static string dump (WebSocketFrame frame)
    {
      var len = frame.Length;
      var cnt = (long) (len / 4);
      var rem = (int) (len % 4);

      int cntDigit;
      string cntFmt;
      if (cnt < 10000) {
        cntDigit = 4;
        cntFmt = "{0,4}";
      }
      else if (cnt < 0x010000) {
        cntDigit = 4;
        cntFmt = "{0,4:X}";
      }
      else if (cnt < 0x0100000000) {
        cntDigit = 8;
        cntFmt = "{0,8:X}";
      }
      else {
        cntDigit = 16;
        cntFmt = "{0,16:X}";
      }

      var spFmt = String.Format ("{{0,{0}}}", cntDigit);
      var headerFmt = String.Format (@"
{0} 01234567 89ABCDEF 01234567 89ABCDEF
{0}+--------+--------+--------+--------+\n", spFmt);
      var lineFmt = String.Format ("{0}|{{1,8}} {{2,8}} {{3,8}} {{4,8}}|\n", cntFmt);
      var footerFmt = String.Format ("{0}+--------+--------+--------+--------+", spFmt);

      var output = new StringBuilder (64);
      Func<Action<string, string, string, string>> linePrinter = () => {
        long lineCnt = 0;
        return (arg1, arg2, arg3, arg4) =>
          output.AppendFormat (lineFmt, ++lineCnt, arg1, arg2, arg3, arg4);
      };

      output.AppendFormat (headerFmt, String.Empty);

      var printLine = linePrinter ();
      var bytes = frame.ToByteArray ();
      for (long i = 0; i <= cnt; i++) {
        var j = i * 4;
        if (i < cnt)
          printLine (
            Convert.ToString (bytes[j],     2).PadLeft (8, '0'),
            Convert.ToString (bytes[j + 1], 2).PadLeft (8, '0'),
            Convert.ToString (bytes[j + 2], 2).PadLeft (8, '0'),
            Convert.ToString (bytes[j + 3], 2).PadLeft (8, '0'));
        else if (rem > 0)
          printLine (
            Convert.ToString (bytes[j], 2).PadLeft (8, '0'),
            rem >= 2 ? Convert.ToString (bytes[j + 1], 2).PadLeft (8, '0') : String.Empty,
            rem == 3 ? Convert.ToString (bytes[j + 2], 2).PadLeft (8, '0') : String.Empty,
            String.Empty);
      }

      output.AppendFormat (footerFmt, String.Empty);
      return output.ToString ();
    }

    private static bool isControl (Opcode opcode)
    {
      return opcode == Opcode.Close || opcode == Opcode.Ping || opcode == Opcode.Pong;
    }

    private static bool isData (Opcode opcode)
    {
      return opcode == Opcode.Text || opcode == Opcode.Binary;
    }

    private static string print (WebSocketFrame frame)
    {
      /* Opcode */

      var opcode = frame._opcode.ToString ();

      /* Payload Length */

      var payloadLen = frame._payloadLength;

      /* Extended Payload Length */

      var extPayloadLen = payloadLen < 126
                          ? String.Empty
                          : payloadLen == 126
                            ? frame._extPayloadLength.ToUInt16 (ByteOrder.Big).ToString ()
                            : frame._extPayloadLength.ToUInt64 (ByteOrder.Big).ToString ();

      /* Masking Key */

      var masked = frame.IsMasked;
      var maskingKey = masked ? BitConverter.ToString (frame._maskingKey) : String.Empty;

      /* Payload Data */

      var payload = payloadLen == 0
                    ? String.Empty
                    : payloadLen > 125
                      ? String.Format ("A {0} frame.", opcode.ToLower ())
                      : !masked && !frame.IsFragmented && !frame.IsCompressed && frame.IsText
                        ? Encoding.UTF8.GetString (frame._payloadData.ApplicationData)
                        : frame._payloadData.ToString ();

      var fmt = @"
                    FIN: {0}
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
        frame._fin,
        frame._rsv1,
        frame._rsv2,
        frame._rsv3,
        opcode,
        frame._mask,
        payloadLen,
        extPayloadLen,
        maskingKey,
        payload);
    }

    private static WebSocketFrame read (byte[] header, Stream stream, bool unmask)
    {
      /* Header */

      // FIN
      var fin = (header[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;
      // RSV1
      var rsv1 = (header[0] & 0x40) == 0x40 ? Rsv.On : Rsv.Off;
      // RSV2
      var rsv2 = (header[0] & 0x20) == 0x20 ? Rsv.On : Rsv.Off;
      // RSV3
      var rsv3 = (header[0] & 0x10) == 0x10 ? Rsv.On : Rsv.Off;
      // Opcode
      var opcode = (Opcode) (header[0] & 0x0f);
      // MASK
      var mask = (header[1] & 0x80) == 0x80 ? Mask.Mask : Mask.Unmask;
      // Payload Length
      var payloadLen = (byte) (header[1] & 0x7f);

      // Check if valid header
      var err = isControl (opcode) && payloadLen > 125
                ? "A control frame has a payload data which is greater than the allowable max size."
                : isControl (opcode) && fin == Fin.More
                  ? "A control frame is fragmented."
                  : !isData (opcode) && rsv1 == Rsv.On
                    ? "A non data frame is compressed."
                    : null;

      if (err != null)
        throw new WebSocketException (CloseStatusCode.ProtocolError, err);

      var frame = new WebSocketFrame ();
      frame._fin = fin;
      frame._rsv1 = rsv1;
      frame._rsv2 = rsv2;
      frame._rsv3 = rsv3;
      frame._opcode = opcode;
      frame._mask = mask;
      frame._payloadLength = payloadLen;

      /* Extended Payload Length */

      var size = payloadLen < 126
                 ? 0
                 : payloadLen == 126
                   ? 2
                   : 8;

      var extPayloadLen = size > 0 ? stream.ReadBytes (size) : new byte[0];
      if (size > 0 && extPayloadLen.Length != size)
        throw new WebSocketException (
          "The 'Extended Payload Length' of a frame cannot be read from the data source.");

      frame._extPayloadLength = extPayloadLen;

      /* Masking Key */

      var masked = mask == Mask.Mask;
      var maskingKey = masked ? stream.ReadBytes (4) : new byte[0];
      if (masked && maskingKey.Length != 4)
        throw new WebSocketException (
          "The 'Masking Key' of a frame cannot be read from the data source.");

      frame._maskingKey = maskingKey;

      /* Payload Data */

      ulong len = payloadLen < 126
                  ? payloadLen
                  : payloadLen == 126
                    ? extPayloadLen.ToUInt16 (ByteOrder.Big)
                    : extPayloadLen.ToUInt64 (ByteOrder.Big);

      byte[] data = null;
      if (len > 0) {
        // Check if allowable max length.
        if (payloadLen > 126 && len > PayloadData.MaxLength)
          throw new WebSocketException (
            CloseStatusCode.TooBig,
            "The length of 'Payload Data' of a frame is greater than the allowable max length.");

        data = payloadLen > 126
               ? stream.ReadBytes ((long) len, 1024)
               : stream.ReadBytes ((int) len);

        if (data.LongLength != (long) len)
          throw new WebSocketException (
            "The 'Payload Data' of a frame cannot be read from the data source.");
      }
      else {
        data = new byte[0];
      }

      frame._payloadData = new PayloadData (data, masked);
      if (unmask && masked)
        frame.Unmask ();

      return frame;
    }

    #endregion

    #region Internal Methods

    internal static WebSocketFrame CreateCloseFrame (PayloadData payloadData, bool mask)
    {
      return new WebSocketFrame (Fin.Final, Opcode.Close, payloadData, false, mask);
    }

    internal static WebSocketFrame CreatePingFrame (bool mask)
    {
      return new WebSocketFrame (Fin.Final, Opcode.Ping, new PayloadData (), false, mask);
    }

    internal static WebSocketFrame CreatePingFrame (byte[] data, bool mask)
    {
      return new WebSocketFrame (Fin.Final, Opcode.Ping, new PayloadData (data), false, mask);
    }

    internal static WebSocketFrame Read (Stream stream)
    {
      return Read (stream, true);
    }

    internal static WebSocketFrame Read (Stream stream, bool unmask)
    {
      var header = stream.ReadBytes (2);
      if (header.Length != 2)
        throw new WebSocketException (
          "The header part of a frame cannot be read from the data source.");

      return read (header, stream, unmask);
    }

    internal static void ReadAsync (
      Stream stream, Action<WebSocketFrame> completed, Action<Exception> error)
    {
      ReadAsync (stream, true, completed, error);
    }

    internal static void ReadAsync (
      Stream stream, bool unmask, Action<WebSocketFrame> completed, Action<Exception> error)
    {
      stream.ReadBytesAsync (
        2,
        header => {
          if (header.Length != 2)
            throw new WebSocketException (
              "The header part of a frame cannot be read from the data source.");

          var frame = read (header, stream, unmask);
          if (completed != null)
            completed (frame);
        },
        error);
    }

    internal void Unmask ()
    {
      if (_mask == Mask.Unmask)
        return;

      _mask = Mask.Unmask;
      _payloadData.Mask (_maskingKey);
      _maskingKey = new byte[0];
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator ()
    {
      foreach (var b in ToByteArray ())
        yield return b;
    }

    public void Print (bool dumped)
    {
      Console.WriteLine (dumped ? dump (this) : print (this));
    }

    public string PrintToString (bool dumped)
    {
      return dumped
             ? dump (this)
             : print (this);
    }

    public byte[] ToByteArray ()
    {
      using (var buff = new MemoryStream ()) {
        var header = (int) _fin;
        header = (header << 1) + (int) _rsv1;
        header = (header << 1) + (int) _rsv2;
        header = (header << 1) + (int) _rsv3;
        header = (header << 4) + (int) _opcode;
        header = (header << 1) + (int) _mask;
        header = (header << 7) + (int) _payloadLength;
        buff.Write (((ushort) header).InternalToByteArray (ByteOrder.Big), 0, 2);

        if (_payloadLength > 125)
          buff.Write (_extPayloadLength, 0, _extPayloadLength.Length);

        if (_mask == Mask.Mask)
          buff.Write (_maskingKey, 0, _maskingKey.Length);

        if (_payloadLength > 0) {
          var payload = _payloadData.ToByteArray ();
          if (_payloadLength < 127)
            buff.Write (payload, 0, payload.Length);
          else
            buff.WriteBytes (payload);
        }

        buff.Close ();
        return buff.ToArray ();
      }
    }

    public override string ToString ()
    {
      return BitConverter.ToString (ToByteArray ());
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
