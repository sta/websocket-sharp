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
using System.Collections.Generic;
using System.Text;

namespace WebSocketSharp.Frame
{
  public class WsFrame : IEnumerable<byte>
  {
    #region Private Static Fields

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

    #region Private Constructors

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
      Fin    = fin;
      Opcode = opcode;
      Masked = mask;

      ulong dataLength = payloadData.Length;
      if (dataLength < 126)
      {
        PayloadLen = (byte)dataLength;
      }
      else if (dataLength < 0x010000)
      {
        PayloadLen    = (byte)126;
        ExtPayloadLen = ((ushort)dataLength).ToBytes(ByteOrder.BIG);
      }
      else
      {
        PayloadLen    = (byte)127;
        ExtPayloadLen = dataLength.ToBytes(ByteOrder.BIG);
      }

      PayloadData = payloadData;
      if (Masked == Mask.MASK)
      {
        MaskingKey = new byte[4];
        var rand   = new Random();
        rand.NextBytes(MaskingKey);
        PayloadData.Mask(MaskingKey);
      }
    }

    #endregion

    #region Public Static Methods

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

    public static WsFrame Parse<TStream>(TStream stream)
      where TStream : System.IO.Stream
    {
      return Parse(stream, true);
    }

    public static WsFrame Parse<TStream>(TStream stream, bool unmask)
      where TStream : System.IO.Stream
    {
      Fin         fin;
      Rsv         rsv1, rsv2, rsv3;
      Opcode      opcode;
      Mask        masked;
      byte        payloadLen;
      byte[]      extPayloadLen = new byte[]{};
      byte[]      maskingKey    = new byte[]{};
      PayloadData payloadData;

      byte[] buffer1, buffer2, buffer3;
      int    buffer1Len    = 2;
      int    buffer2Len    = 0;
      ulong  buffer3Len    = 0;
      int    maskingKeyLen = 4;
      int    readLen       = 0;

      buffer1 = new byte[buffer1Len];
      readLen = stream.Read(buffer1, 0, buffer1Len);
      if (readLen < buffer1Len)
      {
        return null;
      }

      // FIN
      fin = (buffer1[0] & 0x80) == 0x80
          ? Fin.FINAL
          : Fin.MORE;
      // RSV1
      rsv1 = (buffer1[0] & 0x40) == 0x40
           ? Rsv.ON
           : Rsv.OFF;
      // RSV2
      rsv2 = (buffer1[0] & 0x20) == 0x20
           ? Rsv.ON
           : Rsv.OFF;
      // RSV3
      rsv3 = (buffer1[0] & 0x10) == 0x10
           ? Rsv.ON
           : Rsv.OFF;
      // opcode
      opcode = (Opcode)(buffer1[0] & 0x0f);
      // MASK
      masked = (buffer1[1] & 0x80) == 0x80
             ? Mask.MASK
             : Mask.UNMASK;
      // Payload len
      payloadLen = (byte)(buffer1[1] & 0x7f);
      // Extended payload length
      if (payloadLen <= 125)
      {
        buffer3Len = payloadLen;
      }
      else if (payloadLen == 126)
      {
        buffer2Len = 2;
      }
      else
      {
        buffer2Len = 8;
      }

      if (buffer2Len > 0)
      {
        buffer2 = new byte[buffer2Len];
        readLen = stream.Read(buffer2, 0, buffer2Len);

        if (readLen < buffer2Len)
        {
          return null;
        }

        extPayloadLen = buffer2;
        switch (buffer2Len)
        {
          case 2:
            buffer3Len = extPayloadLen.To<ushort>(ByteOrder.BIG);
            break;
          case 8:
            buffer3Len = extPayloadLen.To<ulong>(ByteOrder.BIG);
            break;
        }
      }

      if (buffer3Len > PayloadData.MaxLength)
      {
        throw new WsReceivedTooBigMessageException();
      }
      // Masking-key
      if (masked == Mask.MASK)
      {
        maskingKey = new byte[maskingKeyLen];
        readLen    = stream.Read(maskingKey, 0, maskingKeyLen);

        if (readLen < maskingKeyLen)
        {
          return null;
        }
      }
      // Payload Data
      if (buffer3Len <= (ulong)_readBufferLen)
      {
        buffer3 = new byte[buffer3Len];
        readLen = stream.Read(buffer3, 0, (int)buffer3Len);

        if (readLen < (int)buffer3Len)
        {
          return null;
        }
      }
      else
      {
        buffer3 = stream.ReadBytes(buffer3Len, _readBufferLen);

        if ((ulong)buffer3.LongLength < buffer3Len)
        {
          return null;
        }
      }

      if (masked == Mask.MASK)
      {
        payloadData = new PayloadData(buffer3, true);
        if (unmask == true)
        {
          payloadData.Mask(maskingKey);
          masked     = Mask.UNMASK;
          maskingKey = new byte[]{};
        }
      }
      else
      {
        payloadData = new PayloadData(buffer3);
      }

      return new WsFrame
             {
               Fin           = fin,
               Rsv1          = rsv1,
               Rsv2          = rsv2,
               Rsv3          = rsv3,
               Opcode        = opcode,
               Masked        = masked,
               PayloadLen    = payloadLen,
               ExtPayloadLen = extPayloadLen,
               MaskingKey    = maskingKey,
               PayloadData   = payloadData
             };
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator()
    {
      foreach (byte b in ToBytes())
      {
        yield return b;
      }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
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
      var bytes = new List<byte>();

      int first16 = (int)Fin;
      first16 = (first16 << 1) + (int)Rsv1;
      first16 = (first16 << 1) + (int)Rsv2;
      first16 = (first16 << 1) + (int)Rsv3;
      first16 = (first16 << 4) + (int)Opcode;
      first16 = (first16 << 1) + (int)Masked;
      first16 = (first16 << 7) + (int)PayloadLen;
      bytes.AddRange(((ushort)first16).ToBytes(ByteOrder.BIG));

      if (PayloadLen >= 126)
      {
        bytes.AddRange(ExtPayloadLen);
      }

      if (Masked == Mask.MASK)
      {
        bytes.AddRange(MaskingKey);
      }

      if (PayloadLen > 0)
      {
        bytes.AddRange(PayloadData.ToBytes());
      }

      return bytes.ToArray();
    }

    public override string ToString()
    {
      return BitConverter.ToString(ToBytes());
    }

    #endregion
  }
}
