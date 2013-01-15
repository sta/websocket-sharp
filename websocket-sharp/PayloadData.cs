#region MIT License
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
using System.Linq;
using System.Text;

namespace WebSocketSharp {

  internal class PayloadData : IEnumerable<byte>
  {
    #region Field

    public const ulong MaxLength = long.MaxValue;

    #endregion

    #region Public Constructors

    public PayloadData(string appData)
      : this(Encoding.UTF8.GetBytes(appData))
    {
    }

    public PayloadData(byte[] appData)
      : this(new byte[]{}, appData)
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
        throw new ArgumentOutOfRangeException("Plus 'extData' length and 'appData' lenght must be less than MaxLength.");

      ExtensionData   = extData;
      ApplicationData = appData;
      IsMasked        = masked;
    }

    #endregion

    #region Properties

    public byte[] ExtensionData   { get; private set; }
    public byte[] ApplicationData { get; private set; }

    public bool IsMasked { get; private set; }

    public ulong Length {
      get {
        return (ulong)(ExtensionData.LongLength + ApplicationData.LongLength);
      }
    }

    #endregion

    #region Private Methods

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    private void mask(byte[] src, byte[] key)
    {
      for (long i = 0; i < src.LongLength; i++)
        src[i] = (byte)(src[i] ^ key[i % 4]);
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
        throw new ArgumentOutOfRangeException("maskingKey", "'maskingKey' length must be 4.");

      if (ExtensionData.LongLength > 0)
        mask(ExtensionData, maskingKey);

      if (ApplicationData.LongLength > 0)
        mask(ApplicationData, maskingKey);

      IsMasked = !IsMasked;
    }

    public byte[] ToBytes()
    {
      return ExtensionData.LongLength > 0
             ? ExtensionData.Concat(ApplicationData).ToArray()
             : ApplicationData;
    }

    public override string ToString()
    {
      return BitConverter.ToString(ToBytes());
    }

    #endregion
  }
}
