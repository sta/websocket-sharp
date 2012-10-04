#region MIT License
/**
 * WsFrame.cs
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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace WebSocketSharp.Frame
{
  public class WsFrame : IEnumerable<byte>
  {
    #region Field

    private static readonly int _readBufferLen;

    #endregion

    #region Properties

    public Fin         Fin           { get; private set; }
    public Rsv         Rsv1          { get; private set; }
    public Rsv         Rsv2          { get; private set; }
    public Rsv         Rsv3          { get; private set; }
    public Opcode      Opcode        { get; private set; }
    public Mask        Masked        { get; private set; }
    public byte        PayloadLen    { get; private set; }
    public byte[]      ExtPayloadLen { get; private set; }
    public byte[]      MaskingKey    { get; private set; }
    public PayloadData PayloadData   { get; private set; }

    public ulong Length
    {
      get
      {
        return 2 + (ulong)(ExtPayloadLen.Length + MaskingKey.Length) + PayloadLength;
      }
    }

    public ulong PayloadLength
    {
      get
      {
        return PayloadData.Length;
      }
    }

    #endregion

    #region Static Constructor

    static WsFrame()
    {
      _readBufferLen = 1024;
    }

    #endregion

    #region Private Constructor

    private WsFrame()
    {
      Rsv1          = Rsv.OFF;
      Rsv2          = Rsv.OFF;
      Rsv3          = Rsv.OFF;
      ExtPayloadLen = new byte[]{};
      MaskingKey    = new byte[]{};
    }

    #endregion

    #region Public Constructors

    public WsFrame(Opcode opcode, PayloadData payloadData)
    : this(Fin.FINAL, opcode, payloadData)
    {
    }

    public WsFrame(Fin fin, Opcode opcode, PayloadData payloadData)
    : this(fin, opcode, Mask.MASK, payloadData)
    {
    }

    public WsFrame(Fin fin, Opcode opcode, Mask mask, PayloadData payloadData)
    : this()
    {
      Fin         = fin;
      Opcode      = opcode;
      Masked      = payloadData.Length != 0 ? mask : Mask.UNMASK;
      PayloadData = payloadData;

      init();
    }

    #endregion

    #region Private Methods

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    private void init()
    {
      setPayloadLen(PayloadLength);
      if (Masked == Mask.MASK)
        maskPayloadData();
    }

    private void maskPayloadData()
    {
      var key  = new byte[4];
      var rand = new Random();
      rand.NextBytes(key);

      MaskingKey = key;
      PayloadData.Mask(key);
    }

    private static void readExtPayloadLen(Stream stream, WsFrame frame)
    {
      var length = frame.PayloadLen <= 125
                 ? 0
                 : frame.PayloadLen == 126 ? 2 : 8;

      if (length > 0)
      {
        var extLength = stream.ReadBytes(length);
        if (extLength == null)
          throw new IOException();
        frame.ExtPayloadLen = extLength;
      }
    }

    private static WsFrame readHeader(Stream stream)
    {
      var header = stream.ReadBytes(2);
      if (header == null)
        return null;

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
      Mask masked = (header[1] & 0x80) == 0x80 ? Mask.MASK : Mask.UNMASK;
      // Payload len
      byte payloadLen = (byte)(header[1] & 0x7f);

      return new WsFrame {
        Fin        = fin,
        Rsv1       = rsv1,
        Rsv2       = rsv2,
        Rsv3       = rsv3,
        Opcode     = opcode,
        Masked     = masked,
        PayloadLen = payloadLen};
    }

    private static void readMaskingKey(Stream stream, WsFrame frame)
    {
      if (frame.Masked == Mask.MASK)
      {
        var maskingKey = stream.ReadBytes(4);
        if (maskingKey == null)
          throw new IOException();
        frame.MaskingKey = maskingKey;
      }
    }

    private static void readPayloadData(Stream stream, WsFrame frame, bool unmask)
    {
      ulong length = frame.PayloadLen <= 125
                   ? frame.PayloadLen
                   : frame.PayloadLen == 126
                     ? frame.ExtPayloadLen.To<ushort>(ByteOrder.BIG)
                     : frame.ExtPayloadLen.To<ulong>(ByteOrder.BIG);

      var buffer = length <= (ulong)_readBufferLen
                 ? stream.ReadBytes((int)length)
                 : stream.ReadBytes((long)length, _readBufferLen);

      if (buffer == null)
        throw new IOException();

      PayloadData payloadData;
      if (frame.Masked == Mask.MASK)
      {
        payloadData = new PayloadData(buffer, true);
        if (unmask == true)
        {
          payloadData.Mask(frame.MaskingKey);
          frame.Masked     = Mask.UNMASK;
          frame.MaskingKey = new byte[]{};
        }
      }
      else
      {
        payloadData = new PayloadData(buffer);
      }

      frame.PayloadData = payloadData;
    }

    private void setPayloadLen(ulong length)
    {
      if (length < 126)
      {
        PayloadLen = (byte)length;
        return;
      }

      if (length < 0x010000)
      {
        PayloadLen    = (byte)126;
        ExtPayloadLen = ((ushort)length).ToBytes(ByteOrder.BIG);
        return;
      }

      PayloadLen    = (byte)127;
      ExtPayloadLen = length.ToBytes(ByteOrder.BIG);
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator()
    {
      foreach (byte b in ToBytes())
        yield return b;
    }

    public static WsFrame Parse(byte[] src)
    {
      return Parse(src, true);
    }

    public static WsFrame Parse(byte[] src, bool unmask)
    {
      using (MemoryStream ms = new MemoryStream(src))
      {
        return Parse(ms, unmask);
      }
    }

    public static WsFrame Parse(Stream stream)
    {
      return Parse(stream, true);
    }

    public static WsFrame Parse(Stream stream, bool unmask)
    {
      var frame = readHeader(stream);
      if (frame == null)
        return null;

      readExtPayloadLen(stream, frame);
      readMaskingKey(stream, frame);
      readPayloadData(stream, frame, unmask);

      return frame;
    }

    public void Print()
    {
      byte[] buffer;
      long   count, i, j;
      int    countDigit, remainder;
      string countFmt, extPayloadLen, headerFmt, topLineFmt, bottomLineFmt, payloadData, spFmt;

      switch (ExtPayloadLen.Length)
      {
        case 2:
          extPayloadLen = ExtPayloadLen.To<ushort>(ByteOrder.BIG).ToString();
          break;
        case 8:
          extPayloadLen = ExtPayloadLen.To<ulong>(ByteOrder.BIG).ToString();
          break;
        default:
          extPayloadLen = String.Empty;
          break;
      }

      if (((Opcode.TEXT | Opcode.PING | Opcode.PONG) & Opcode) == Opcode &&
          Masked == Mask.UNMASK &&
          PayloadLength > 0)
      {
        payloadData = Encoding.UTF8.GetString(PayloadData.ToBytes());
      }
      else
      {
        payloadData = BitConverter.ToString(PayloadData.ToBytes());
      }

      headerFmt = @"
 WsFrame:

 FIN={0}, RSV1={1}, RSV2={2}, RSV3={3}, Opcode={4},
 MASK={5}, Payload Len={6}, Extended Payload Len={7},
 Masking Key ={8},
 Payload Data={9}";

      buffer    = ToBytes();
      count     = (long)(Length / 4);
      remainder = (int)(Length % 4);

      if (count < 10000)
      {
        countDigit = 4;
        countFmt   = "{0,4}";
      }
      else if (count < 0x010000)
      {
        countDigit = 4;
        countFmt   = "{0,4:X}";
      }
      else if (count < 0x0100000000)
      {
        countDigit = 8;
        countFmt   = "{0,8:X}";
      }
      else
      {
        countDigit = 16;
        countFmt   = "{0,16:X}";
      }

      spFmt = String.Format("{{0,{0}}}", countDigit);

      topLineFmt = String.Format(@"
 {0} 01234567 89ABCDEF 01234567 89ABCDEF
 {0}+--------+--------+--------+--------+", spFmt);

      Func<string, Action<string, string, string, string>> func = s =>
      {
        long   lineCount = 0;
        string lineFmt   = String.Format(" {0}|{{1,8}} {{2,8}} {{3,8}} {{4,8}}|", s);
        return (arg1, arg2, arg3, arg4) =>
        {
          Console.WriteLine(lineFmt, ++lineCount, arg1, arg2, arg3, arg4);
        };
      };
      var printLine = func(countFmt);

      bottomLineFmt = String.Format(" {0}+--------+--------+--------+--------+", spFmt);

      Console.WriteLine(headerFmt,
        Fin, Rsv1, Rsv2, Rsv3, Opcode,
        Masked, PayloadLen, extPayloadLen,
        BitConverter.ToString(MaskingKey),
        payloadData);

      Console.WriteLine(topLineFmt, String.Empty);

      for (i = 0; i <= count; i++)
      {
        j = i * 4;
        if (i < count)
        {
          printLine(
            Convert.ToString(buffer[j],     2).PadLeft(8, '0'),
            Convert.ToString(buffer[j + 1], 2).PadLeft(8, '0'),
            Convert.ToString(buffer[j + 2], 2).PadLeft(8, '0'),
            Convert.ToString(buffer[j + 3], 2).PadLeft(8, '0'));
        }
        else if (i == count && remainder > 0)
        {
          printLine(
            Convert.ToString(buffer[j], 2).PadLeft(8, '0'),
            remainder >= 2 ? Convert.ToString(buffer[j + 1], 2).PadLeft(8, '0') : String.Empty,
            remainder == 3 ? Convert.ToString(buffer[j + 2], 2).PadLeft(8, '0') : String.Empty,
            String.Empty);
        }
      }

      Console.WriteLine(bottomLineFmt, String.Empty);
    }

    public byte[] ToBytes()
    {
      var buffer = new List<byte>();

      int header = (int)Fin;
      header = (header << 1) + (int)Rsv1;
      header = (header << 1) + (int)Rsv2;
      header = (header << 1) + (int)Rsv3;
      header = (header << 4) + (int)Opcode;
      header = (header << 1) + (int)Masked;
      header = (header << 7) + (int)PayloadLen;
      buffer.AddRange(((ushort)header).ToBytes(ByteOrder.BIG));

      if (PayloadLen >= 126)
        buffer.AddRange(ExtPayloadLen);

      if (Masked == Mask.MASK)
        buffer.AddRange(MaskingKey);

      if (PayloadLen > 0)
        buffer.AddRange(PayloadData.ToBytes());

      return buffer.ToArray();
    }

    public override string ToString()
    {
      return BitConverter.ToString(ToBytes());
    }

    #endregion
  }
}
