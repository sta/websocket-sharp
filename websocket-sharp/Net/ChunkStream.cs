#region License
/*
 * ChunkStream.cs
 *
 * This code is derived from System.Net.ChunkStream.cs of Mono
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
    private bool                _gotit;
    private WebHeaderCollection _headers;
    private StringBuilder       _saved;
    private bool                _sawCR;
    private InputChunkState     _state;
    private int                 _trailerState;

    #endregion

    #region Public Constructors

    public ChunkStream (byte [] buffer, int offset, int size, WebHeaderCollection headers)
      : this (headers)
    {
      Write (buffer, offset, size);
    }

    public ChunkStream (WebHeaderCollection headers)
    {
      _headers = headers;
      _chunkSize = -1;
      _chunks = new List<Chunk> ();
      _saved = new StringBuilder ();
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

    private InputChunkState readCRLF (byte [] buffer, ref int offset, int size)
    {
      if (!_sawCR) {
        if ((char) buffer [offset++] != '\r')
          throwProtocolViolation ("Expecting \\r.");

        _sawCR = true;
        if (offset == size)
          return InputChunkState.BodyFinished;
      }

      if ((char) buffer [offset++] != '\n')
        throwProtocolViolation ("Expecting \\n.");

      return InputChunkState.None;
    }

    private int readFromChunks (byte [] buffer, int offset, int size)
    {
      var count = _chunks.Count;
      var nread = 0;
      for (int i = 0; i < count; i++) {
        var chunk = _chunks [i];
        if (chunk == null)
          continue;

        if (chunk.ReadLeft == 0) {
          _chunks [i] = null;
          continue;
        }

        nread += chunk.Read (buffer, offset + nread, size - nread);
        if (nread == size)
          break;
      }

      return nread;
    }

    private InputChunkState readTrailer (byte [] buffer, ref int offset, int size)
    {
      var c = '\0';

      // Short path
      if (_trailerState == 2 && (char) buffer [offset] == '\r' && _saved.Length == 0) {
        offset++;
        if (offset < size && (char) buffer [offset] == '\n') {
          offset++;
          return InputChunkState.None;
        }

        offset--;
      }

      var st = _trailerState;
      var stString = "\r\n\r";
      while (offset < size && st < 4) {
        c = (char) buffer [offset++];
        if ((st == 0 || st == 2) && c == '\r') {
          st++;
          continue;
        }

        if ((st == 1 || st == 3) && c == '\n') {
          st++;
          continue;
        }

        if (st > 0) {
          _saved.Append (stString.Substring (0, _saved.Length == 0 ? st - 2 : st));
          st = 0;
          if (_saved.Length > 4196)
            throwProtocolViolation ("Error reading trailer (too long).");
        }
      }

      if (st < 4) {
        _trailerState = st;
        if (offset < size)
          throwProtocolViolation ("Error reading trailer.");

        return InputChunkState.Trailer;
      }

      var reader = new StringReader (_saved.ToString ());
      string line;
      while ((line = reader.ReadLine ()) != null && line != "")
        _headers.Add (line);

      return InputChunkState.None;
    }

    private static string removeChunkExtension (string input)
    {
      var idx = input.IndexOf (';');
      return idx > -1
             ? input.Substring (0, idx)
             : input;
    }

    private InputChunkState setChunkSize (byte [] buffer, ref int offset, int size)
    {
      var c = '\0';
      while (offset < size) {
        c = (char) buffer [offset++];
        if (c == '\r') {
          if (_sawCR)
            throwProtocolViolation ("2 CR found.");

          _sawCR = true;
          continue;
        }
        
        if (_sawCR && c == '\n')
          break;

        if (c == ' ')
          _gotit = true;

        if (!_gotit)
          _saved.Append (c);

        if (_saved.Length > 20)
          throwProtocolViolation ("Chunk size too long.");
      }

      if (!_sawCR || c != '\n') {
        if (offset < size)
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
      var ex = new WebException (message, null, WebExceptionStatus.ServerProtocolViolation, null);
      throw ex;
    }

    private void write (byte [] buffer, ref int offset, int size)
    {
      if (_state == InputChunkState.None) {
        _state = setChunkSize (buffer, ref offset, size);
        if (_state == InputChunkState.None)
          return;

        _saved.Length = 0;
        _sawCR = false;
        _gotit = false;
      }

      if (_state == InputChunkState.Body && offset < size) {
        _state = writeBody (buffer, ref offset, size);
        if (_state == InputChunkState.Body)
          return;
      }

      if (_state == InputChunkState.BodyFinished && offset < size) {
        _state = readCRLF (buffer, ref offset, size);
        if (_state == InputChunkState.BodyFinished)
          return;

        _sawCR = false;
      }
      
      if (_state == InputChunkState.Trailer && offset < size) {
        _state = readTrailer (buffer, ref offset, size);
        if (_state == InputChunkState.Trailer)
          return;

        _saved.Length = 0;
        _sawCR = false;
        _gotit = false;
      }

      if (offset < size)
        write (buffer, ref offset, size);
    }

    private InputChunkState writeBody (byte [] buffer, ref int offset, int size)
    {
      if (_chunkSize == 0)
        return InputChunkState.BodyFinished;

      var diff = size - offset;
      if (diff + _chunkRead > _chunkSize)
        diff = _chunkSize - _chunkRead;

      var body = new byte [diff];
      Buffer.BlockCopy (buffer, offset, body, 0, diff);
      _chunks.Add (new Chunk (body));

      offset += diff;
      _chunkRead += diff;

      return _chunkRead == _chunkSize
             ? InputChunkState.BodyFinished
             : InputChunkState.Body;
    }

    #endregion

    #region Public Methods

    public int Read (byte [] buffer, int offset, int size)
    {
      return readFromChunks (buffer, offset, size);
    }

    public void ResetBuffer ()
    {
      _chunkSize = -1;
      _chunkRead = 0;
      _chunks.Clear ();
    }

    public void Write (byte [] buffer, int offset, int size)
    {
      write (buffer, ref offset, size);
    }

    public void WriteAndReadBack (byte [] buffer, int offset, int size, ref int read)
    {
      if (offset + read > 0)
        Write (buffer, offset, offset + read);

      read = readFromChunks (buffer, offset, size);
    }

    #endregion
  }
}
