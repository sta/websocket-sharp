#region License
/*
 * PayloadData.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace WebSocketSharp {

  internal class PayloadData : IEnumerable<byte>
  {
    #region Public Fields

    public const ulong MaxLength = long.MaxValue;

    #endregion

    #region Public Constructors

    public PayloadData()
      : this(new byte[]{})
    {
    }

    public PayloadData(byte[] appData)
      : this(new byte[]{}, appData)
    {
    }

    public PayloadData(string appData)
      : this(Encoding.UTF8.GetBytes(appData))
    {
    }

    public PayloadData(byte[] appData, bool masked)
      : this(new byte[]{}, appData, masked)
    {
    }

    public PayloadData(byte[] extData, byte[] appData)
      : this(extData, appData, false)
    {
    }

    public PayloadData(byte[] extData, byte[] appData, bool masked)
    {
      if (extData.IsNull())
        throw new ArgumentNullException("extData");

      if (appData.IsNull())
        throw new ArgumentNullException("appData");

      if ((ulong)extData.LongLength + (ulong)appData.LongLength > MaxLength)
        throw new ArgumentOutOfRangeException(
          "The length of 'extData' plus 'appData' must be less than MaxLength.");

      ExtensionData = extData;
      ApplicationData = appData;
      IsMasked = masked;
    }

    #endregion

    #region Internal Properties

    internal bool ContainsReservedCloseStatusCode {
      get {
        if (Length >= 2)
        {
          var code = ToByteArray().SubArray(0, 2).To<ushort>(ByteOrder.BIG);
          if (code == (ushort)CloseStatusCode.UNDEFINED ||
              code == (ushort)CloseStatusCode.NO_STATUS_CODE ||
              code == (ushort)CloseStatusCode.ABNORMAL ||
              code == (ushort)CloseStatusCode.TLS_HANDSHAKE_FAILURE)
            return true;
        }

        return false;
      }
    }

    internal bool IsMasked { get; private set; }

    internal ulong Length {
      get {
        return (ulong)(ExtensionData.LongLength + ApplicationData.LongLength);
      }
    }

    #endregion

    #region Public Properties

    public byte[] ExtensionData { get; private set; }

    public byte[] ApplicationData { get; private set; }

    #endregion

    #region Private Methods

    private static byte[] compress(byte[] value)
    {
      if (value.LongLength == 0)
        //return new Byte[] { 0x00, 0x00, 0x00, 0xff, 0xff };
        return value;

      using (var comp = new MemoryStream())
      using (var ds = new DeflateStream(comp, CompressionMode.Compress, true))
      {
        ds.Write(value, 0, value.Length);
        ds.Close(); // "BFINAL" set to 1.
        comp.Close();

        return comp.ToArray();
      }
    }

    private static byte[] decompress(byte[] value)
    {
      if (value.LongLength == 0)
        return value;

      using (var decomp = new MemoryStream())
      using (var comp = new MemoryStream(value))
      using (var ds = new DeflateStream(comp, CompressionMode.Decompress, true))
      {
        int readLen = 0;
        var buffer = new byte[256];
        while (true)
        {
          readLen = ds.Read(buffer, 0, buffer.Length);
          if (readLen == 0)
            break;

          decomp.Write(buffer, 0, readLen);
        }

        decomp.Close();
        return decomp.ToArray();
      }
    }

    private static void mask(byte[] src, byte[] key)
    {
      for (long i = 0; i < src.LongLength; i++)
        src[i] = (byte)(src[i] ^ key[i % 4]);
    }

    #endregion

    #region Internal Methods

    internal bool Compress(CompressionMethod method)
    {
      try
      {
        if (ExtensionData.LongLength > 0)
          return false;

        if (method == CompressionMethod.DEFLATE)
          ApplicationData = compress(ApplicationData);
        else
          return false;

        return true;
      }
      catch
      {
        return false;
      }
    }

    internal bool Decompress(CompressionMethod method)
    {
      try
      {
        if (ApplicationData.LongLength == 0)
          return true;

        if (method == CompressionMethod.DEFLATE)
          ApplicationData = decompress(ApplicationData);
        else
          return false;

        return true;
      }
      catch
      {
        return false;
      }
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator()
    {
      foreach (byte b in ExtensionData)
        yield return b;

      foreach (byte b in ApplicationData)
        yield return b;
    }

    public void Mask(byte[] maskingKey)
    {
      if (maskingKey.IsNull())
        throw new ArgumentNullException("maskingKey");

      if (maskingKey.Length != 4)
        throw new ArgumentOutOfRangeException("maskingKey", "The length must be 4.");

      if (ExtensionData.LongLength > 0)
        mask(ExtensionData, maskingKey);

      if (ApplicationData.LongLength > 0)
        mask(ApplicationData, maskingKey);

      IsMasked = !IsMasked;
    }

    public byte[] ToByteArray()
    {
      return ExtensionData.LongLength > 0
             ? this.ToArray()
             : ApplicationData;
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
