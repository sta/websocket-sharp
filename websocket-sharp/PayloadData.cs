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

    private byte[] _appData;
    private ulong  _appDataLength;
    private byte[] _extData;
    private ulong  _extDataLength;
    private bool   _masked;

    #endregion

    #region Public Fields

    public const ulong MaxLength = Int64.MaxValue;

    #endregion

    #region Internal Constructors

    internal PayloadData ()
      : this (new byte[0], new byte[0], false)
    {
    }

    internal PayloadData (byte[] applicationData)
      : this (new byte[0], applicationData, false)
    {
    }

    internal PayloadData (string applicationData)
      : this (new byte[0], Encoding.UTF8.GetBytes (applicationData), false)
    {
    }

    internal PayloadData (byte[] applicationData, bool masked)
      : this (new byte[0], applicationData, masked)
    {
    }

    internal PayloadData (byte[] extensionData, byte[] applicationData, bool masked)
    {
      _extDataLength = (ulong) extensionData.LongLength;
      _appDataLength = (ulong) applicationData.LongLength;
      if (_appDataLength > MaxLength - _extDataLength)
        throw new ArgumentOutOfRangeException (
          "The total length of 'extensionData' and 'applicationData' is greater than the allowable length.");

      _extData = extensionData;
      _appData = applicationData;
      _masked = masked;
    }

    #endregion

    #region Internal Properties

    internal bool IncludesReservedCloseStatusCode {
      get {
        return _appDataLength > 1 &&
               _appData.SubArray (0, 2).ToUInt16 (ByteOrder.Big).IsReserved ();
      }
    }

    #endregion

    #region Public Properties

    public byte[] ApplicationData {
      get {
        return _appData;
      }
    }

    public byte[] ExtensionData {
      get {
        return _extData;
      }
    }

    public bool IsMasked {
      get {
        return _masked;
      }
    }

    public ulong Length {
      get {
        return _extDataLength + _appDataLength;
      }
    }

    #endregion

    #region Private Methods

    private static void mask (byte[] source, byte[] key)
    {
      for (long i = 0; i < source.LongLength; i++)
        source[i] = (byte) (source[i] ^ key[i % 4]);
    }

    #endregion

    #region Internal Methods

    internal void Mask (byte[] key)
    {
      if (_extDataLength > 0)
        mask (_extData, key);

      if (_appDataLength > 0)
        mask (_appData, key);

      _masked = !_masked;
    }

    #endregion

    #region Public Methods

    public IEnumerator<byte> GetEnumerator ()
    {
      foreach (var b in _extData)
        yield return b;

      foreach (var b in _appData)
        yield return b;
    }

    public byte[] ToByteArray ()
    {
      return _extDataLength > 0
             ? new List<byte> (this).ToArray ()
             : _appData;
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
