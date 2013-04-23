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

namespace WebSocketSharp {

  internal class WsFrame : IEnumerable<byte>
  {
    #region Private Const Fields

    private const int _readBufferLen = 1024;

    #endregion

    #region Private Constructors

    private WsFrame()
    {
    }

    #endregion

    #region Public Constructors

    public WsFrame(Opcode opcode, PayloadData payloadData)
      : this(opcode, Mask.MASK, payloadData)
    {
    }

    public WsFrame(Opcode opcode, Mask mask, PayloadData payloadData)
      : this(Fin.FINAL, opcode, mask, payloadData)
    {
    }

    public WsFrame(Fin fin, Opcode opcode, Mask mask, PayloadData payloadData)
      : this(fin, opcode, mask, payloadData, CompressionMethod.NONE)
    {
    }

    public WsFrame(
      Fin fin, Opcode opcode, Mask mask, PayloadData payloadData, CompressionMethod compress)
    {
      if (payloadData.IsNull())
        throw new ArgumentNullException("payloadData");

      if (isControl(opcode) && payloadData.Length > 125)
        throw new ArgumentOutOfRangeException("payloadData",
          "The control frame must have a payload length of 125 bytes or less.");

      if (!isFinal(fin) && isControl(opcode))
        throw new ArgumentException("The control frame must not be fragmented.");

      if (isControl(opcode) && compress != CompressionMethod.NONE)
        throw new ArgumentException("The control frame must not be compressed.");

      Fin = fin;
      Rsv1 = Rsv.OFF;
      Rsv2 = Rsv.OFF;
      Rsv3 = Rsv.OFF;
      Opcode = opcode;
      Mask = mask;
      PayloadData = payloadData;
      if (compress != CompressionMethod.NONE)
        compressPayloadData(compress);

      init();
    }

    #endregion

    #region Internal Properties

    internal bool IsBinary {
      get {
        return isBinary(Opcode);
      }
    }

    internal bool IsClose {
      get {
        return isClose(Opcode);
      }
    }

    internal bool IsCompressed {
      get {
        return Rsv1 == Rsv.ON;
      }
    }

    internal bool IsContinuation {
      get {
        return isContinuation(Opcode);
      }
    }

    internal bool IsControl {
      get {
        return isControl(Opcode);
      }
    }

    internal bool IsData {
      get {
        return isData(Opcode);
      }
    }

    internal bool IsFinal {
      get {
        return isFinal(Fin);
      }
    }

    internal bool IsFragmented {
      get {
        return !IsFinal || IsContinuation;
      }
    }

    internal bool IsMasked {
      get {
        return isMasked(Mask);
      }
    }

    internal bool IsPing {
      get {
        return isPing(Opcode);
      }
    }

    internal bool IsPong {
      get {
        return isPong(Opcode);
      }
    }

    internal bool IsText {
      get {
        return isText(Opcode);
      }
    }

    internal ulong Length {
      get {
        return 2 + (ulong)(ExtPayloadLen.Length + MaskingKey.Length) + PayloadData.Length;
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

    public byte[] ExtPayloadLen { get; private set; }

    public byte[] MaskingKey { get; private set; }

    public PayloadData PayloadData { get; private set; }

    #endregion

    #region Private Methods

    private bool compressPayloadData(CompressionMethod method)
    {
      if (!PayloadData.Compress(method))
        return false;

      if (IsData)
        Rsv1 = Rsv.ON;

      return true;
    }

    private bool decompressPayloadData(CompressionMethod method)
    {
      if (!PayloadData.Decompress(method))
        return false;

      Rsv1 = Rsv.OFF;
      return true;
    }

    private static void dump(WsFrame frame)
    {
      var len = frame.Length;
      var count = (long)(len / 4);
      var remainder = (int)(len % 4);

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

      var spFmt = String.Format("{{0,{0}}}", countDigit);
      var headerFmt = String.Format(@"
 {0} 01234567 89ABCDEF 01234567 89ABCDEF
 {0}+--------+--------+--------+--------+", spFmt);
      var footerFmt = String.Format(" {0}+--------+--------+--------+--------+", spFmt);

      Func<Action<string, string, string, string>> linePrinter = () =>
      {
        long lineCount = 0;
        var lineFmt = String.Format(" {0}|{{1,8}} {{2,8}} {{3,8}} {{4,8}}|", countFmt);
        return (arg1, arg2, arg3, arg4) =>
        {
          Console.WriteLine(lineFmt, ++lineCount, arg1, arg2, arg3, arg4);
        };
      };
      var printLine = linePrinter();

      Console.WriteLine(headerFmt, String.Empty);

      var buffer = frame.ToByteArray();
      int i, j;
      for (i = 0; i <= count; i++)
      {
        j = i * 4;
        if (i < count)
          printLine(
            Convert.ToString(buffer[j],     2).PadLeft(8, '0'),
            Convert.ToString(buffer[j + 1], 2).PadLeft(8, '0'),
            Convert.ToString(buffer[j + 2], 2).PadLeft(8, '0'),
            Convert.ToString(buffer[j + 3], 2).PadLeft(8, '0'));
        else if (remainder > 0)
          printLine(
            Convert.ToString(buffer[j], 2).PadLeft(8, '0'),
            remainder >= 2 ? Convert.ToString(buffer[j + 1], 2).PadLeft(8, '0') : String.Empty,
            remainder == 3 ? Convert.ToString(buffer[j + 2], 2).PadLeft(8, '0') : String.Empty,
            String.Empty);
      }

      Console.WriteLine(footerFmt, String.Empty);
    }

    private void init()
    {
      setPayloadLen(PayloadData.Length);
      if (IsMasked)
        maskPayloadData();
      else
        MaskingKey = new byte[]{};
    }

    private static bool isBinary(Opcode opcode)
    {
      return opcode == Opcode.BINARY;
    }

    private static bool isClose(Opcode opcode)
    {
      return opcode == Opcode.CLOSE;
    }

    private static bool isContinuation(Opcode opcode)
    {
      return opcode == Opcode.CONT;
    }

    private static bool isControl(Opcode opcode)
    {
      return isClose(opcode) || isPing(opcode) || isPong(opcode);
    }

    private static bool isData(Opcode opcode)
    {
      return isText(opcode) || isBinary(opcode);
    }

    private static bool isFinal(Fin fin)
    {
      return fin == Fin.FINAL;
    }

    private static bool isMasked(Mask mask)
    {
      return mask == Mask.MASK;
    }

    private static bool isPing(Opcode opcode)
    {
      return opcode == Opcode.PING;
    }

    private static bool isPong(Opcode opcode)
    {
      return opcode == Opcode.PONG;
    }

    private static bool isText(Opcode opcode)
    {
      return opcode == Opcode.TEXT;
    }

    private void maskPayloadData()
    {
      var key = new byte[4];
      var rand = new Random();
      rand.NextBytes(key);

      MaskingKey = key;
      PayloadData.Mask(key);
    }

    private static WsFrame parse(byte[] header, Stream stream, bool unmask)
    {
      if (header.IsNull() || header.Length != 2)
        return null;

      var frame = readHeader(header);
      readExtPayloadLen(stream, frame);
      readMaskingKey(stream, frame);
      readPayloadData(stream, frame, unmask);

      return frame;
    }

    private static void print(WsFrame frame)
    {
      var len = frame.ExtPayloadLen.Length;
      var extPayloadLen = len == 2
                        ? frame.ExtPayloadLen.To<ushort>(ByteOrder.BIG).ToString()
                        : len == 8
                          ? frame.ExtPayloadLen.To<ulong>(ByteOrder.BIG).ToString()
                          : String.Empty;

      var masked = frame.IsMasked;
      var maskingKey = masked
                     ? BitConverter.ToString(frame.MaskingKey)
                     : String.Empty;

      var opcode = frame.Opcode;
      var payloadData = frame.PayloadData.Length == 0
                      ? String.Empty
                      : masked || frame.IsFragmented || frame.IsBinary || frame.IsClose
                        ? BitConverter.ToString(frame.PayloadData.ToByteArray())
                        : Encoding.UTF8.GetString(frame.PayloadData.ToByteArray());

      var format = @"
 FIN: {0}
 RSV1: {1}
 RSV2: {2}
 RSV3: {3}
 Opcode: {4}
 MASK: {5}
 Payload Len: {6}
 Extended Payload Len: {7}
 Masking Key: {8}
 Payload Data: {9}";

      Console.WriteLine(
        format, frame.Fin, frame.Rsv1, frame.Rsv2, frame.Rsv3, opcode, frame.Mask, frame.PayloadLen, extPayloadLen, maskingKey, payloadData);
    }

    private static void readExtPayloadLen(Stream stream, WsFrame frame)
    {
      int length = frame.PayloadLen <= 125
                 ? 0
                 : frame.PayloadLen == 126
                   ? 2
                   : 8;

      if (length == 0)
      {
        frame.ExtPayloadLen = new byte[]{};
        return;
      }

      var extPayloadLen = stream.ReadBytes(length);
      if (extPayloadLen.Length != length)
        throw new IOException();

      frame.ExtPayloadLen = extPayloadLen;
    }

    private static WsFrame readHeader(byte[] header)
    {
      // FIN
      Fin fin = (header[0] & 0x80) == 0x80 ? Fin.FINAL : Fin.MORE;
      // RSV1
      Rsv rsv1 = (header[0] & 0x40) == 0x40 ? Rsv.ON : Rsv.OFF;
      // RSV2
      Rsv rsv2 = (header[0] & 0x20) == 0x20 ? Rsv.ON : Rsv.OFF;
      // RSV3
      Rsv rsv3 = (header[0] & 0x10) == 0x10 ? Rsv.ON : Rsv.OFF;
      // Opcode
      Opcode opcode = (Opcode)(header[0] & 0x0f);
      // MASK
      Mask mask = (header[1] & 0x80) == 0x80 ? Mask.MASK : Mask.UNMASK;
      // Payload len
      byte payloadLen = (byte)(header[1] & 0x7f);

      return new WsFrame {
        Fin = fin,
        Rsv1 = rsv1,
        Rsv2 = rsv2,
        Rsv3 = rsv3,
        Opcode = opcode,
        Mask = mask,
        PayloadLen = payloadLen
      };
    }

    private static void readMaskingKey(Stream stream, WsFrame frame)
    {
      if (!isMasked(frame.Mask))
      {
        frame.MaskingKey = new byte[]{};
        return;
      }

      var maskingKey = stream.ReadBytes(4);
      if (maskingKey.Length != 4)
        throw new IOException();

      frame.MaskingKey = maskingKey;
    }

    private static void readPayloadData(Stream stream, WsFrame frame, bool unmask)
    {
      ulong length = frame.PayloadLen <= 125
                   ? frame.PayloadLen
                   : frame.PayloadLen == 126
                     ? frame.ExtPayloadLen.To<ushort>(ByteOrder.BIG)
                     : frame.ExtPayloadLen.To<ulong>(ByteOrder.BIG);

      if (length == 0)
      {
        frame.PayloadData = new PayloadData();
        return;
      }

      if (frame.PayloadLen > 126 && length > PayloadData.MaxLength)
        throw new WsReceivedTooBigMessageException();

      var buffer = length <= (ulong)_readBufferLen
                 ? stream.ReadBytes((int)length)
                 : stream.ReadBytes((long)length, _readBufferLen);

      if (buffer.LongLength != (long)length)
        throw new IOException();

      var masked = isMasked(frame.Mask);
      var payloadData = masked
                      ? new PayloadData(buffer, true)
                      : new PayloadData(buffer);

      if (masked && unmask)
      {
        payloadData.Mask(frame.MaskingKey);
        frame.Mask = Mask.UNMASK;
        frame.MaskingKey = new byte[]{};
      }

      frame.PayloadData = payloadData;
    }

    private void setPayloadLen(ulong length)
    {
      if (length < 126)
      {
        PayloadLen = (byte)length;
        ExtPayloadLen = new byte[]{};
        return;
      }

      if (length < 0x010000)
      {
        PayloadLen = (byte)126;
        ExtPayloadLen = ((ushort)length).ToByteArray(ByteOrder.BIG);
        return;
      }

      PayloadLen = (byte)127;
      ExtPayloadLen = length.ToByteArray(ByteOrder.BIG);
    }

    private void unmaskPayloadData()
    {
      PayloadData.Mask(MaskingKey);
      Mask = Mask.UNMASK;
      MaskingKey = new byte[]{};
    }

    #endregion

    #region Internal Methods

    internal void Decompress(CompressionMethod method)
    {
      if (Mask == Mask.MASK)
        unmaskPayloadData();

      if (decompressPayloadData(method))
        setPayloadLen(PayloadData.Length);
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator()
    {
      foreach (byte b in ToByteArray())
        yield return b;
    }

    public static WsFrame Parse(byte[] src)
    {
      return Parse(src, true);
    }

    public static WsFrame Parse(Stream stream)
    {
      return Parse(stream, true);
    }

    public static WsFrame Parse(byte[] src, bool unmask)
    {
      using (MemoryStream ms = new MemoryStream(src))
      {
        return Parse(ms, unmask);
      }
    }

    public static WsFrame Parse(Stream stream, bool unmask)
    {
      return Parse(stream, unmask, null);
    }

    public static WsFrame Parse(Stream stream, bool unmask, Action<Exception> error)
    {
      WsFrame frame = null;
      try
      {
        var header = stream.ReadBytes(2);
        frame = parse(header, stream, unmask);
      }
      catch (Exception ex)
      {
        if (!error.IsNull())
          error(ex);
      }

      return frame;
    }

    public static void ParseAsync(Stream stream, Action<WsFrame> completed)
    {
      ParseAsync(stream, true, completed, null);
    }

    public static void ParseAsync(Stream stream, Action<WsFrame> completed, Action<Exception> error)
    {
      ParseAsync(stream, true, completed, error);
    }

    public static void ParseAsync(
      Stream stream, bool unmask, Action<WsFrame> completed, Action<Exception> error)
    {
      var header = new byte[2];
      AsyncCallback callback = ar =>
      {
        WsFrame frame = null;
        try
        {
          int readLen = stream.EndRead(ar);
          if (readLen != 2)
          {
            if (readLen == 1)
              header[1] = (byte)stream.ReadByte();
            else
              header = null;
          }

          frame = parse(header, stream, unmask);
        }
        catch (Exception ex)
        {
          if (!error.IsNull())
            error(ex);
        }
        finally
        {
          if (!completed.IsNull())
            completed(frame);
        }
      };

      stream.BeginRead(header, 0, 2, callback, null);
    }

    public void Print(bool dumped)
    {
      if (dumped)
        dump(this);
      else
        print(this);
    }

    public byte[] ToByteArray()
    {
      var buffer = new List<byte>();

      int header = (int)Fin;
      header = (header << 1) + (int)Rsv1;
      header = (header << 1) + (int)Rsv2;
      header = (header << 1) + (int)Rsv3;
      header = (header << 4) + (int)Opcode;
      header = (header << 1) + (int)Mask;
      header = (header << 7) + (int)PayloadLen;
      buffer.AddRange(((ushort)header).ToByteArray(ByteOrder.BIG));

      if (PayloadLen >= 126)
        buffer.AddRange(ExtPayloadLen);

      if (IsMasked)
        buffer.AddRange(MaskingKey);

      if (PayloadLen > 0)
        buffer.AddRange(PayloadData.ToByteArray());

      return buffer.ToArray();
    }

    public override string ToString()
    {
      return BitConverter.ToString(ToByteArray());
    }

    #endregion

    #region Explicitly Implemented Interface Members

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    #endregion
  }
}
