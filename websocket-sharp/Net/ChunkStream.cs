#region License
/*
 * ChunkStream.cs
 *
 * This code is derived from ChunkStream.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2003 Ximian, Inc (http://www.ximian.com)
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

#region Authors
/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@ximian.com>
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace WebSocketSharp.Net
{
  internal class ChunkStream
  {
    #region Private Fields

    private int                 _chunkRead;
    private int                 _chunkSize;
    private List<Chunk>         _chunks;
    private bool                _gotIt;
    private WebHeaderCollection _headers;
    private StringBuilder       _saved;
    private bool                _sawCr;
    private InputChunkState     _state;
    private int                 _trailerState;

    #endregion

    #region Public Constructors

    public ChunkStream (WebHeaderCollection headers)
    {
      _headers = headers;
      _chunkSize = -1;
      _chunks = new List<Chunk> ();
      _saved = new StringBuilder ();
    }

    public ChunkStream (byte[] buffer, int offset, int count, WebHeaderCollection headers)
      : this (headers)
    {
      Write (buffer, offset, count);
    }

    #endregion

    #region Internal Properties

    internal WebHeaderCollection Headers {
      get {
        return _headers;
      }
    }

    #endregion

    #region Public Properties

    public int ChunkLeft {
      get {
        return _chunkSize - _chunkRead;
      }
    }

    public bool WantMore {
      get {
        return _chunkRead != _chunkSize || _chunkSize != 0 || _state != InputChunkState.None;
      }
    }

    #endregion

    #region Private Methods

    private InputChunkState readCrLfFrom (byte[] buffer, ref int offset, int count)
    {
      if (!_sawCr) {
        if ((char) buffer[offset++] != '\r')
          throwProtocolViolation ("Expecting \\r.");

        _sawCr = true;
        if (offset == count)
          return InputChunkState.BodyFinished;
      }

      if ((char) buffer[offset++] != '\n')
        throwProtocolViolation ("Expecting \\n.");

      return InputChunkState.None;
    }

    private int readFromChunks (byte[] buffer, int offset, int count)
    {
      var cnt = _chunks.Count;
      var nread = 0;
      for (var i = 0; i < cnt; i++) {
        var chunk = _chunks[i];
        if (chunk == null)
          continue;

        if (chunk.ReadLeft == 0) {
          _chunks[i] = null;
          continue;
        }

        nread += chunk.Read (buffer, offset + nread, count - nread);
        if (nread == count)
          break;
      }

      return nread;
    }

    private InputChunkState readTrailerFrom (byte[] buffer, ref int offset, int count)
    {
      var c = '\0';

      // Short path.
      if (_trailerState == 2 && (char) buffer[offset] == '\r' && _saved.Length == 0) {
        offset++;
        if (offset < count && (char) buffer[offset] == '\n') {
          offset++;
          return InputChunkState.None;
        }

        offset--;
      }

      var state = _trailerState;
      var stateStr = "\r\n\r";
      while (offset < count && state < 4) {
        c = (char) buffer[offset++];
        if ((state == 0 || state == 2) && c == '\r') {
          state++;
          continue;
        }

        if ((state == 1 || state == 3) && c == '\n') {
          state++;
          continue;
        }

        if (state > 0) {
          _saved.Append (stateStr.Substring (0, _saved.Length == 0 ? state - 2 : state));
          state = 0;
          if (_saved.Length > 4196)
            throwProtocolViolation ("Error reading trailer (too long).");
        }
      }

      if (state < 4) {
        _trailerState = state;
        if (offset < count)
          throwProtocolViolation ("Error reading trailer.");

        return InputChunkState.Trailer;
      }

      var reader = new StringReader (_saved.ToString ());
      string line;
      while ((line = reader.ReadLine ()) != null && line != "")
        _headers.Add (line);

      return InputChunkState.None;
    }

    private static string removeChunkExtension (string value)
    {
      var idx = value.IndexOf (';');
      return idx > -1 ? value.Substring (0, idx) : value;
    }

    private InputChunkState setChunkSize (byte[] buffer, ref int offset, int count)
    {
      var c = '\0';
      while (offset < count) {
        c = (char) buffer[offset++];
        if (c == '\r') {
          if (_sawCr)
            throwProtocolViolation ("2 CR found.");

          _sawCr = true;
          continue;
        }

        if (_sawCr && c == '\n')
          break;

        if (c == ' ')
          _gotIt = true;

        if (!_gotIt)
          _saved.Append (c);

        if (_saved.Length > 20)
          throwProtocolViolation ("Chunk size too long.");
      }

      if (!_sawCr || c != '\n') {
        if (offset < count)
          throwProtocolViolation ("Missing \\n.");

        try {
          if (_saved.Length > 0)
            _chunkSize = Int32.Parse (
              removeChunkExtension (_saved.ToString ()), NumberStyles.HexNumber);
        }
        catch {
          throwProtocolViolation ("Cannot parse chunk size.");
        }

        return InputChunkState.None;
      }

      _chunkRead = 0;
      try {
        _chunkSize = Int32.Parse (
          removeChunkExtension (_saved.ToString ()), NumberStyles.HexNumber);
      }
      catch {
        throwProtocolViolation ("Cannot parse chunk size.");
      }

      if (_chunkSize == 0) {
        _trailerState = 2;
        return InputChunkState.Trailer;
      }

      return InputChunkState.Body;
    }

    private static void throwProtocolViolation (string message)
    {
      throw new WebException (message, null, WebExceptionStatus.ServerProtocolViolation, null);
    }

    private void write (byte[] buffer, ref int offset, int count)
    {
      if (_state == InputChunkState.None) {
        _state = setChunkSize (buffer, ref offset, count);
        if (_state == InputChunkState.None)
          return;

        _saved.Length = 0;
        _sawCr = false;
        _gotIt = false;
      }

      if (_state == InputChunkState.Body && offset < count) {
        _state = writeBody (buffer, ref offset, count);
        if (_state == InputChunkState.Body)
          return;
      }

      if (_state == InputChunkState.BodyFinished && offset < count) {
        _state = readCrLfFrom (buffer, ref offset, count);
        if (_state == InputChunkState.BodyFinished)
          return;

        _sawCr = false;
      }

      if (_state == InputChunkState.Trailer && offset < count) {
        _state = readTrailerFrom (buffer, ref offset, count);
        if (_state == InputChunkState.Trailer)
          return;

        _saved.Length = 0;
        _sawCr = false;
        _gotIt = false;
      }

      if (offset < count)
        write (buffer, ref offset, count);
    }

    private InputChunkState writeBody (byte[] buffer, ref int offset, int count)
    {
      if (_chunkSize == 0)
        return InputChunkState.BodyFinished;

      var diff = count - offset;
      if (diff + _chunkRead > _chunkSize)
        diff = _chunkSize - _chunkRead;

      var body = new byte[diff];
      Buffer.BlockCopy (buffer, offset, body, 0, diff);
      _chunks.Add (new Chunk (body));

      offset += diff;
      _chunkRead += diff;

      return _chunkRead == _chunkSize ? InputChunkState.BodyFinished : InputChunkState.Body;
    }

    #endregion

    #region Public Methods

    public int Read (byte[] buffer, int offset, int count)
    {
      return readFromChunks (buffer, offset, count);
    }

    public void ResetBuffer ()
    {
      _chunkSize = -1;
      _chunkRead = 0;
      _chunks.Clear ();
    }

    public void Write (byte[] buffer, int offset, int count)
    {
      write (buffer, ref offset, count);
    }

    public void WriteAndReadBack (byte[] buffer, int offset, int count, ref int read)
    {
      if (offset + read > 0)
        Write (buffer, offset, offset + read);

      read = readFromChunks (buffer, offset, count);
    }

    #endregion
  }
}
