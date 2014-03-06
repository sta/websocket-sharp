#region License
/*
 * WsFrame.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2013 sta.blockhead
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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace WebSocketSharp
{
  internal class WsFrame : IEnumerable<byte>
  {
    #region Internal Static Fields

    internal static readonly byte [] EmptyUnmaskPingData;

    #endregion

    #region Static Constructor

    static WsFrame ()
    {
      EmptyUnmaskPingData = CreatePingFrame (Mask.Unmask).ToByteArray ();
    }

    #endregion

    #region Private Constructors

    private WsFrame ()
    {
    }

    #endregion

    #region Public Constructors

    public WsFrame (Opcode opcode, PayloadData payload)
      : this (opcode, Mask.Mask, payload)
    {
    }

    public WsFrame (Opcode opcode, Mask mask, PayloadData payload)
      : this (Fin.Final, opcode, mask, payload)
    {
    }

    public WsFrame (Fin fin, Opcode opcode, Mask mask, PayloadData payload)
      : this (fin, opcode, mask, payload, false)
    {
    }

    public WsFrame (
      Fin fin, Opcode opcode, Mask mask, PayloadData payload, bool compressed)
    {
      Fin = fin;
      Rsv1 = isData (opcode) && compressed ? Rsv.On : Rsv.Off;
      Rsv2 = Rsv.Off;
      Rsv3 = Rsv.Off;
      Opcode = opcode;
      Mask = mask;

      /* PayloadLen */

      var dataLen = payload.Length;
      var payloadLen = dataLen < 126
                     ? (byte) dataLen
                     : dataLen < 0x010000
                       ? (byte) 126
                       : (byte) 127;

      PayloadLen = payloadLen;

      /* ExtPayloadLen */

      ExtPayloadLen = payloadLen < 126
                    ? new byte []{}
                    : payloadLen == 126
                      ? ((ushort) dataLen).ToByteArrayInternally (ByteOrder.Big)
                      : dataLen.ToByteArrayInternally (ByteOrder.Big);

      /* MaskingKey */

      var masking = mask == Mask.Mask;
      var maskingKey = masking
                     ? createMaskingKey ()
                     : new byte []{};

      MaskingKey = maskingKey;

      /* PayloadData */

      if (masking)
        payload.Mask (maskingKey);

      PayloadData = payload;
    }

    #endregion

    #region Internal Properties

    internal bool IsBinary {
      get {
        return Opcode == Opcode.Binary;
      }
    }

    internal bool IsClose {
      get {
        return Opcode == Opcode.Close;
      }
    }

    internal bool IsCompressed {
      get {
        return Rsv1 == Rsv.On;
      }
    }

    internal bool IsContinuation {
      get {
        return Opcode == Opcode.Cont;
      }
    }

    internal bool IsControl {
      get {
        var opcode = Opcode;
        return opcode == Opcode.Close || opcode == Opcode.Ping || opcode == Opcode.Pong;
      }
    }

    internal bool IsData {
      get {
        var opcode = Opcode;
        return opcode == Opcode.Binary || opcode == Opcode.Text;
      }
    }

    internal bool IsFinal {
      get {
        return Fin == Fin.Final;
      }
    }

    internal bool IsFragmented {
      get {
        return Fin == Fin.More || Opcode == Opcode.Cont;
      }
    }

    internal bool IsMasked {
      get {
        return Mask == Mask.Mask;
      }
    }

    internal bool IsPerMessageCompressed {
      get {
        var opcode = Opcode;
        return (opcode == Opcode.Binary || opcode == Opcode.Text) && Rsv1 == Rsv.On;
      }
    }

    internal bool IsPing {
      get {
        return Opcode == Opcode.Ping;
      }
    }

    internal bool IsPong {
      get {
        return Opcode == Opcode.Pong;
      }
    }

    internal bool IsText {
      get {
        return Opcode == Opcode.Text;
      }
    }

    internal ulong Length {
      get {
        return 2 + (ulong) (ExtPayloadLen.Length + MaskingKey.Length) + PayloadData.Length;
      }
    }

    #endregion

    #region Public Properties

    public Fin Fin { get; private set; }

    public Rsv Rsv1 { get; private set; }

    public Rsv Rsv2 { get; private set; }

    public Rsv Rsv3 { get; private set; }

    public Opcode Opcode { get; private set; }

    public Mask Mask { get; private set; }

    public byte PayloadLen { get; private set; }

    public byte [] ExtPayloadLen { get; private set; }

    public byte [] MaskingKey { get; private set; }

    public PayloadData PayloadData { get; private set; }

    #endregion

    #region Private Methods

    private static byte [] createMaskingKey ()
    {
      var key = new byte [4];
      var rand = new Random ();
      rand.NextBytes (key);

      return key;
    }

    private static string dump (WsFrame frame)
    {
      var len = frame.Length;
      var count = (long) (len / 4);
      var rem = (int) (len % 4);

      int countDigit;
      string countFmt;
      if (count < 10000)
      {
        countDigit = 4;
        countFmt = "{0,4}";
      }
      else if (count < 0x010000)
      {
        countDigit = 4;
        countFmt = "{0,4:X}";
      }
      else if (count < 0x0100000000)
      {
        countDigit = 8;
        countFmt = "{0,8:X}";
      }
      else
      {
        countDigit = 16;
        countFmt = "{0,16:X}";
      }

      var spFmt = String.Format ("{{0,{0}}}", countDigit);
      var headerFmt = String.Format (
@"{0} 01234567 89ABCDEF 01234567 89ABCDEF
{0}+--------+--------+--------+--------+\n", spFmt);
      var footerFmt = String.Format ("{0}+--------+--------+--------+--------+", spFmt);

      var buffer = new StringBuilder (64);
      Func<Action<string, string, string, string>> linePrinter = () =>
      {
        long lineCount = 0;
        var lineFmt = String.Format ("{0}|{{1,8}} {{2,8}} {{3,8}} {{4,8}}|\n", countFmt);
        return (arg1, arg2, arg3, arg4) =>
          buffer.AppendFormat (lineFmt, ++lineCount, arg1, arg2, arg3, arg4);
      };
      var printLine = linePrinter ();

      buffer.AppendFormat (headerFmt, String.Empty);

      var frameAsBytes = frame.ToByteArray ();
      int i, j;
      for (i = 0; i <= count; i++)
      {
        j = i * 4;
        if (i < count)
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

      buffer.AppendFormat (footerFmt, String.Empty);
      return buffer.ToString ();
    }

    private static bool isBinary (Opcode opcode)
    {
      return opcode == Opcode.Binary;
    }

    private static bool isClose (Opcode opcode)
    {
      return opcode == Opcode.Close;
    }

    private static bool isContinuation (Opcode opcode)
    {
      return opcode == Opcode.Cont;
    }

    private static bool isControl (Opcode opcode)
    {
      return opcode == Opcode.Close || opcode == Opcode.Ping || opcode == Opcode.Pong;
    }

    private static bool isData (Opcode opcode)
    {
      return opcode == Opcode.Text || opcode == Opcode.Binary;
    }

    private static bool isFinal (Fin fin)
    {
      return fin == Fin.Final;
    }

    private static bool isMasked (Mask mask)
    {
      return mask == Mask.Mask;
    }

    private static bool isPing (Opcode opcode)
    {
      return opcode == Opcode.Ping;
    }

    private static bool isPong (Opcode opcode)
    {
      return opcode == Opcode.Pong;
    }

    private static bool isText (Opcode opcode)
    {
      return opcode == Opcode.Text;
    }

    private static WsFrame parse (byte [] header, Stream stream, bool unmask)
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
      // Payload len
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
          "The payload data length of a control frame is greater than 125 bytes.");

      var frame = new WsFrame {
        Fin = fin,
        Rsv1 = rsv1,
        Rsv2 = rsv2,
        Rsv3 = rsv3,
        Opcode = opcode,
        Mask = mask,
        PayloadLen = payloadLen
      };

      /* Extended Payload Length */

      var extLen = payloadLen < 126
                 ? 0
                 : payloadLen == 126
                   ? 2
                   : 8;

      var extPayloadLen = extLen > 0
                        ? stream.ReadBytes (extLen)
                        : new byte []{};

      if (extLen > 0 && extPayloadLen.Length != extLen)
        throw new WebSocketException (
          "The 'Extended Payload Length' of a frame cannot be read from the data source.");

      frame.ExtPayloadLen = extPayloadLen;

      /* Masking Key */

      var masked = mask == Mask.Mask;
      var maskingKey = masked
                     ? stream.ReadBytes (4)
                     : new byte []{};

      if (masked && maskingKey.Length != 4)
        throw new WebSocketException (
          "The 'Masking Key' of a frame cannot be read from the data source.");

      frame.MaskingKey = maskingKey;

      /* Payload Data */

      ulong dataLen = payloadLen < 126
                    ? payloadLen
                    : payloadLen == 126
                      ? extPayloadLen.ToUInt16 (ByteOrder.Big)
                      : extPayloadLen.ToUInt64 (ByteOrder.Big);

      byte [] data = null;
      if (dataLen > 0)
      {
        // Check if allowable payload data length.
        if (payloadLen > 126 && dataLen > PayloadData.MaxLength)
          throw new WebSocketException (
            CloseStatusCode.TooBig, "The 'Payload Data' length is greater than the allowable length.");

        data = payloadLen > 126
             ? stream.ReadBytes ((long) dataLen, 1024)
             : stream.ReadBytes ((int) dataLen);

        if (data.LongLength != (long) dataLen)
          throw new WebSocketException (
            "The 'Payload Data' of a frame cannot be read from the data source.");
      }
      else
      {
        data = new byte []{};
      }

      var payload = new PayloadData (data, masked);
      if (masked && unmask)
      {
        payload.Mask (maskingKey);
        frame.Mask = Mask.Unmask;
        frame.MaskingKey = new byte []{};
      }

      frame.PayloadData = payload;
      return frame;
    }

    private static string print (WsFrame frame)
    {
      /* Opcode */

      var opcode = frame.Opcode.ToString ();

      /* Payload Len */

      var payloadLen = frame.PayloadLen;

      /* Extended Payload Len */

      var ext = frame.ExtPayloadLen;
      var size = ext.Length;
      var extLen = size == 2
                 ? ext.ToUInt16 (ByteOrder.Big).ToString ()
                 : size == 8
                   ? ext.ToUInt64 (ByteOrder.Big).ToString ()
                   : String.Empty;

      /* Masking Key */

      var masked = frame.IsMasked;
      var key = masked
              ? BitConverter.ToString (frame.MaskingKey)
              : String.Empty;

      /* Payload Data */

      var data = payloadLen == 0
               ? String.Empty
               : size > 0
                 ? String.Format ("A {0} data with {1} bytes.", opcode.ToLower (), extLen)
                 : masked || frame.IsFragmented || frame.IsBinary || frame.IsClose
                   ? BitConverter.ToString (frame.PayloadData.ToByteArray ())
                   : Encoding.UTF8.GetString (frame.PayloadData.ApplicationData);

      var format =
@"                 FIN: {0}
                RSV1: {1}
                RSV2: {2}
                RSV3: {3}
              Opcode: {4}
                MASK: {5}
         Payload Len: {6}
Extended Payload Len: {7}
         Masking Key: {8}
        Payload Data: {9}";

      return String.Format (
        format,
        frame.Fin,
        frame.Rsv1,
        frame.Rsv2,
        frame.Rsv3,
        opcode,
        frame.Mask,
        payloadLen,
        extLen,
        key,
        data);
    }

    #endregion

    #region Internal Methods

    internal static WsFrame CreateCloseFrame (Mask mask, PayloadData payload)
    {
      return new WsFrame (Opcode.Close, mask, payload);
    }

    internal static WsFrame CreatePongFrame (Mask mask, PayloadData payload)
    {
      return new WsFrame (Opcode.Pong, mask, payload);
    }

    #endregion

    #region Public Methods

    public static WsFrame CreateCloseFrame (Mask mask, byte [] data)
    {
      return new WsFrame (Opcode.Close, mask, new PayloadData (data));
    }

    public static WsFrame CreateCloseFrame (Mask mask, CloseStatusCode code, string reason)
    {
      return new WsFrame (Opcode.Close, mask, new PayloadData (((ushort) code).Append (reason)));
    }

    public static WsFrame CreateFrame (
      Fin fin, Opcode opcode, Mask mask, byte [] data, bool compressed)
    {
      return new WsFrame (fin, opcode, mask, new PayloadData (data), compressed);
    }

    public static WsFrame CreatePingFrame (Mask mask)
    {
      return new WsFrame (Opcode.Ping, mask, new PayloadData ());
    }

    public static WsFrame CreatePingFrame (Mask mask, byte [] data)
    {
      return new WsFrame (Opcode.Ping, mask, new PayloadData (data));
    }

    public IEnumerator<byte> GetEnumerator ()
    {
      foreach (byte b in ToByteArray ())
        yield return b;
    }

    public static WsFrame Parse (byte [] src)
    {
      return Parse (src, true);
    }

    public static WsFrame Parse (Stream stream)
    {
      return Parse (stream, true);
    }

    public static WsFrame Parse (byte [] src, bool unmask)
    {
      using (var stream = new MemoryStream (src))
      {
        return Parse (stream, unmask);
      }
    }

    public static WsFrame Parse (Stream stream, bool unmask)
    {
      var header = stream.ReadBytes (2);
      if (header.Length != 2)
        throw new WebSocketException (
          "The header part of a frame cannot be read from the data source.");

      return parse (header, stream, unmask);
    }

    public static void ParseAsync (Stream stream, Action<WsFrame> completed)
    {
      ParseAsync (stream, true, completed, null);
    }

    public static void ParseAsync (Stream stream, Action<WsFrame> completed, Action<Exception> error)
    {
      ParseAsync (stream, true, completed, error);
    }

    public static void ParseAsync (
      Stream stream, bool unmask, Action<WsFrame> completed, Action<Exception> error)
    {
      stream.ReadBytesAsync (
        2,
        header =>
        {
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
      return dumped ? dump (this) : print (this);
    }

    public byte [] ToByteArray()
    {
      using (var buffer = new MemoryStream ())
      {
        int header = (int) Fin;
        header = (header << 1) + (int) Rsv1;
        header = (header << 1) + (int) Rsv2;
        header = (header << 1) + (int) Rsv3;
        header = (header << 4) + (int) Opcode;
        header = (header << 1) + (int) Mask;
        header = (header << 7) + (int) PayloadLen;
        buffer.Write (((ushort) header).ToByteArrayInternally (ByteOrder.Big), 0, 2);

        if (PayloadLen > 125)
          buffer.Write (ExtPayloadLen, 0, ExtPayloadLen.Length);

        if (Mask == Mask.Mask)
          buffer.Write (MaskingKey, 0, MaskingKey.Length);

        if (PayloadLen > 0)
        {
          var payload = PayloadData.ToByteArray ();
          if (PayloadLen < 127)
            buffer.Write (payload, 0, payload.Length);
          else
            buffer.WriteBytes (payload);
        }

        buffer.Close ();
        return buffer.ToArray ();
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
