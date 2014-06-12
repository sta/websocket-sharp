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

    private byte []     _extPayloadLength;
    private Fin         _fin;
    private Mask        _mask;
    private byte []     _maskingKey;
    private Opcode      _opcode;
    private PayloadData _payloadData;
    private byte        _payloadLength;
    private Rsv         _rsv1;
    private Rsv         _rsv2;
    private Rsv         _rsv3;

    #endregion

    #region Internal Static Fields

    internal static readonly byte [] EmptyUnmaskPingData;

    #endregion

    #region Static Constructor

    static WebSocketFrame ()
    {
      EmptyUnmaskPingData = CreatePingFrame (Mask.Unmask).ToByteArray ();
    }

    #endregion

    #region Private Constructors

    private WebSocketFrame ()
    {
    }

    #endregion

    #region Public Constructors

    public WebSocketFrame (Opcode opcode, PayloadData payload)
      : this (Fin.Final, opcode, Mask.Mask, payload, false)
    {
    }

    public WebSocketFrame (Opcode opcode, Mask mask, PayloadData payload)
      : this (Fin.Final, opcode, mask, payload, false)
    {
    }

    public WebSocketFrame (Fin fin, Opcode opcode, Mask mask, PayloadData payload)
      : this (fin, opcode, mask, payload, false)
    {
    }

    public WebSocketFrame (Fin fin, Opcode opcode, Mask mask, PayloadData payload, bool compressed)
    {
      _fin = fin;
      _rsv1 = isData (opcode) && compressed ? Rsv.On : Rsv.Off;
      _rsv2 = Rsv.Off;
      _rsv3 = Rsv.Off;
      _opcode = opcode;
      _mask = mask;

      var len = payload.Length;
      if (len < 126) {
        _payloadLength = (byte) len;
        _extPayloadLength = new byte [0];
      }
      else if (len < 0x010000) {
        _payloadLength = (byte) 126;
        _extPayloadLength = ((ushort) len).ToByteArrayInternally (ByteOrder.Big);
      }
      else {
        _payloadLength = (byte) 127;
        _extPayloadLength = len.ToByteArrayInternally (ByteOrder.Big);
      }

      if (mask == Mask.Mask) {
        _maskingKey = createMaskingKey ();
        payload.Mask (_maskingKey);
      }
      else {
        _maskingKey = new byte [0];
      }

      _payloadData = payload;
    }

    #endregion

    #region Public Properties

    public byte [] ExtendedPayloadLength {
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

    public byte [] MaskingKey {
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

    private static byte [] createMaskingKey ()
    {
      var key = new byte [4];
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
      var headerFmt = String.Format (
@"{0} 01234567 89ABCDEF 01234567 89ABCDEF
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
      var frameAsBytes = frame.ToByteArray ();
      for (long i = 0; i <= cnt; i++) {
        var j = i * 4;
        if (i < cnt)
          printLine (
            Convert.ToString (frameAsBytes [j],     2).PadLeft (8, '0'),
            Convert.ToString (frameAsBytes [j + 1], 2).PadLeft (8, '0'),
            Convert.ToString (frameAsBytes [j + 2], 2).PadLeft (8, '0'),
            Convert.ToString (frameAsBytes [j + 3], 2).PadLeft (8, '0'));
        else if (rem > 0)
          printLine (
            Convert.ToString (frameAsBytes [j], 2).PadLeft (8, '0'),
            rem >= 2 ? Convert.ToString (frameAsBytes [j + 1], 2).PadLeft (8, '0') : String.Empty,
            rem == 3 ? Convert.ToString (frameAsBytes [j + 2], 2).PadLeft (8, '0') : String.Empty,
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

    private static WebSocketFrame parse (byte [] header, Stream stream, bool unmask)
    {
      /* Header */

      // FIN
      var fin = (header [0] & 0x80) == 0x80 ? Fin.Final : Fin.More;
      // RSV1
      var rsv1 = (header [0] & 0x40) == 0x40 ? Rsv.On : Rsv.Off;
      // RSV2
      var rsv2 = (header [0] & 0x20) == 0x20 ? Rsv.On : Rsv.Off;
      // RSV3
      var rsv3 = (header [0] & 0x10) == 0x10 ? Rsv.On : Rsv.Off;
      // Opcode
      var opcode = (Opcode) (header [0] & 0x0f);
      // MASK
      var mask = (header [1] & 0x80) == 0x80 ? Mask.Mask : Mask.Unmask;
      // Payload Length
      var payloadLen = (byte) (header [1] & 0x7f);

      // Check if correct frame.
      var incorrect = isControl (opcode) && fin == Fin.More
                      ? "A control frame is fragmented."
                      : !isData (opcode) && rsv1 == Rsv.On
                        ? "A non data frame is compressed."
                        : null;

      if (incorrect != null)
        throw new WebSocketException (CloseStatusCode.IncorrectData, incorrect);

      // Check if consistent frame.
      if (isControl (opcode) && payloadLen > 125)
        throw new WebSocketException (
          CloseStatusCode.InconsistentData,
          "The length of payload data of a control frame is greater than 125 bytes.");

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

      var extPayloadLen = size > 0 ? stream.ReadBytes (size) : new byte [0];
      if (size > 0 && extPayloadLen.Length != size)
        throw new WebSocketException (
          "The 'Extended Payload Length' of a frame cannot be read from the data source.");

      frame._extPayloadLength = extPayloadLen;

      /* Masking Key */

      var masked = mask == Mask.Mask;
      var maskingKey = masked ? stream.ReadBytes (4) : new byte [0];
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

      byte [] data = null;
      if (len > 0) {
        // Check if allowable payload data length.
        if (payloadLen > 126 && len > PayloadData.MaxLength)
          throw new WebSocketException (
            CloseStatusCode.TooBig,
            "The length of 'Payload Data' of a frame is greater than the allowable length.");

        data = payloadLen > 126
               ? stream.ReadBytes ((long) len, 1024)
               : stream.ReadBytes ((int) len);

        if (data.LongLength != (long) len)
          throw new WebSocketException (
            "The 'Payload Data' of a frame cannot be read from the data source.");
      }
      else {
        data = new byte [0];
      }

      var payload = new PayloadData (data, masked);
      if (masked && unmask) {
        payload.Mask (maskingKey);
        frame._mask = Mask.Unmask;
        frame._maskingKey = new byte [0];
      }

      frame._payloadData = payload;
      return frame;
    }

    private static string print (WebSocketFrame frame)
    {
      /* Opcode */

      var opcode = frame._opcode.ToString ();

      /* Payload Length */

      var payloadLen = frame._payloadLength;

      /* Extended Payload Length */

      var ext = frame._extPayloadLength;
      var size = ext.Length;
      var extPayloadLen = size == 2
                          ? ext.ToUInt16 (ByteOrder.Big).ToString ()
                          : size == 8
                            ? ext.ToUInt64 (ByteOrder.Big).ToString ()
                            : String.Empty;

      /* Masking Key */

      var masked = frame.IsMasked;
      var maskingKey = masked ? BitConverter.ToString (frame._maskingKey) : String.Empty;

      /* Payload Data */

      var payload = payloadLen == 0
                    ? String.Empty
                    : size > 0
                      ? String.Format ("A {0} frame.", opcode.ToLower ())
                      : !masked && !frame.IsFragmented && frame.IsText
                        ? Encoding.UTF8.GetString (frame._payloadData.ApplicationData)
                        : frame._payloadData.ToString ();

      var format =
@"                    FIN: {0}
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
        format,
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

    #endregion

    #region Internal Methods

    internal static WebSocketFrame CreateCloseFrame (Mask mask, PayloadData payload)
    {
      return new WebSocketFrame (Opcode.Close, mask, payload);
    }

    internal static WebSocketFrame CreatePongFrame (Mask mask, PayloadData payload)
    {
      return new WebSocketFrame (Opcode.Pong, mask, payload);
    }

    #endregion

    #region Public Methods

    public static WebSocketFrame CreateCloseFrame (Mask mask, byte [] data)
    {
      return new WebSocketFrame (Opcode.Close, mask, new PayloadData (data));
    }

    public static WebSocketFrame CreateCloseFrame (Mask mask, CloseStatusCode code, string reason)
    {
      return new WebSocketFrame (
        Opcode.Close, mask, new PayloadData (((ushort) code).Append (reason)));
    }

    public static WebSocketFrame CreateFrame (
      Fin fin, Opcode opcode, Mask mask, byte [] data, bool compressed)
    {
      return new WebSocketFrame (fin, opcode, mask, new PayloadData (data), compressed);
    }

    public static WebSocketFrame CreatePingFrame (Mask mask)
    {
      return new WebSocketFrame (Opcode.Ping, mask, new PayloadData ());
    }

    public static WebSocketFrame CreatePingFrame (Mask mask, byte [] data)
    {
      return new WebSocketFrame (Opcode.Ping, mask, new PayloadData (data));
    }

    public IEnumerator<byte> GetEnumerator ()
    {
      foreach (var b in ToByteArray ())
        yield return b;
    }

    public static WebSocketFrame Parse (byte [] src)
    {
      return Parse (src, true);
    }

    public static WebSocketFrame Parse (Stream stream)
    {
      return Parse (stream, true);
    }

    public static WebSocketFrame Parse (byte [] src, bool unmask)
    {
      using (var stream = new MemoryStream (src))
        return Parse (stream, unmask);
    }

    public static WebSocketFrame Parse (Stream stream, bool unmask)
    {
      var header = stream.ReadBytes (2);
      if (header.Length != 2)
        throw new WebSocketException (
          "The header part of a frame cannot be read from the data source.");

      return parse (header, stream, unmask);
    }

    public static void ParseAsync (Stream stream, Action<WebSocketFrame> completed)
    {
      ParseAsync (stream, true, completed, null);
    }

    public static void ParseAsync (
      Stream stream, Action<WebSocketFrame> completed, Action<Exception> error)
    {
      ParseAsync (stream, true, completed, error);
    }

    public static void ParseAsync (
      Stream stream, bool unmask, Action<WebSocketFrame> completed, Action<Exception> error)
    {
      stream.ReadBytesAsync (
        2,
        header => {
          if (header.Length != 2)
            throw new WebSocketException (
              "The header part of a frame cannot be read from the data source.");

          var frame = parse (header, stream, unmask);
          if (completed != null)
            completed (frame);
        },
        error);
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

    public byte [] ToByteArray ()
    {
      using (var buff = new MemoryStream ()) {
        var header = (int) _fin;
        header = (header << 1) + (int) _rsv1;
        header = (header << 1) + (int) _rsv2;
        header = (header << 1) + (int) _rsv3;
        header = (header << 4) + (int) _opcode;
        header = (header << 1) + (int) _mask;
        header = (header << 7) + (int) _payloadLength;
        buff.Write (((ushort) header).ToByteArrayInternally (ByteOrder.Big), 0, 2);

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

    #region Explicitly Implemented Interface Members

    IEnumerator IEnumerable.GetEnumerator ()
    {
      return GetEnumerator ();
    }

    #endregion
  }
}
