#region License
/*
 * PayloadData.cs
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
using System.Text;

namespace WebSocketSharp
{
  internal class PayloadData : IEnumerable<byte>
  {
    #region Private Fields

    private byte [] _applicationData;
    private byte [] _extensionData;
    private bool    _masked;

    #endregion

    #region Public Const Fields

    public const ulong MaxLength = long.MaxValue;

    #endregion

    #region Public Constructors

    public PayloadData ()
      : this (new byte [0], new byte [0], false)
    {
    }

    public PayloadData (byte [] applicationData)
      : this (new byte [0], applicationData, false)
    {
    }

    public PayloadData (string applicationData)
      : this (new byte [0], Encoding.UTF8.GetBytes (applicationData), false)
    {
    }

    public PayloadData (byte [] applicationData, bool masked)
      : this (new byte [0], applicationData, masked)
    {
    }

    public PayloadData (byte [] extensionData, byte [] applicationData, bool masked)
    {
      if ((ulong) extensionData.LongLength + (ulong) applicationData.LongLength > MaxLength)
        throw new ArgumentOutOfRangeException (
          "The length of 'extensionData' plus 'applicationData' is greater than MaxLength.");

      _extensionData = extensionData;
      _applicationData = applicationData;
      _masked = masked;
    }

    #endregion

    #region Internal Properties

    internal bool ContainsReservedCloseStatusCode {
      get {
        return _applicationData.Length > 1 &&
               _applicationData.SubArray (0, 2).ToUInt16 (ByteOrder.Big).IsReserved ();
      }
    }

    #endregion

    #region Public Properties

    public byte [] ApplicationData {
      get {
        return _applicationData;
      }
    }

    public byte [] ExtensionData {
      get {
        return _extensionData;
      }
    }

    public bool IsMasked {
      get {
        return _masked;
      }
    }

    public ulong Length {
      get {
        return (ulong) (_extensionData.LongLength + _applicationData.LongLength);
      }
    }

    #endregion

    #region Private Methods

    private static void mask (byte [] src, byte [] key)
    {
      for (long i = 0; i < src.LongLength; i++)
        src [i] = (byte) (src [i] ^ key [i % 4]);
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator ()
    {
      foreach (byte b in _extensionData)
        yield return b;

      foreach (byte b in _applicationData)
        yield return b;
    }

    public void Mask (byte [] maskingKey)
    {
      if (_extensionData.LongLength > 0)
        mask (_extensionData, maskingKey);

      if (_applicationData.LongLength > 0)
        mask (_applicationData, maskingKey);

      _masked = !_masked;
    }

    public byte [] ToByteArray ()
    {
      return _extensionData.LongLength > 0
             ? new List<byte> (this).ToArray ()
             : _applicationData;
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
