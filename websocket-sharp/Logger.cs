#region License
/*
 * Logger.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013-2024 sta.blockhead
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
using System.Diagnostics;
using System.IO;

namespace WebSocketSharp
{
  /// <summary>
  /// Provides a set of methods and properties for logging.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   If you output a log with lower than the current logging level,
  ///   it cannot be outputted.
  ///   </para>
  ///   <para>
  ///   The default output method writes a log to the standard output
  ///   stream and the text file if it has a valid path.
  ///   </para>
  ///   <para>
  ///   If you would like to use the custom output method, you should
  ///   specify it with the constructor or the <see cref="Logger.Output"/>
  ///   property.
  ///   </para>
  /// </remarks>
  public class Logger
  {
    #region Private Fields

    private volatile string         _file;
    private volatile LogLevel       _level;
    private Action<LogData, string> _output;
    private object                  _sync;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    /// <remarks>
    /// This constructor initializes the logging level with the Error level.
    /// </remarks>
    public Logger ()
      : this (LogLevel.Error, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class with
    /// the specified logging level.
    /// </summary>
    /// <param name="level">
    /// One of the <see cref="LogLevel"/> enum values that specifies
    /// the logging level.
    /// </param>
    public Logger (LogLevel level)
      : this (level, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class with
    /// the specified logging level, path to the log file, and delegate
    /// used to output a log.
    /// </summary>
    /// <param name="level">
    /// One of the <see cref="LogLevel"/> enum values that specifies
    /// the logging level.
    /// </param>
    /// <param name="file">
    /// A <see cref="string"/> that specifies the path to the log file.
    /// </param>
    /// <param name="output">
    /// An <see cref="T:System.Action{LogData, string}"/> that specifies
    /// the delegate used to output a log.
    /// </param>
    public Logger (LogLevel level, string file, Action<LogData, string> output)
    {
      _level = level;
      _file = file;
      _output = output ?? defaultOutput;

      _sync = new object ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the path to the log file.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the path to the log file if any.
    /// </value>
    public string File {
      get {
        return _file;
      }

      set {
        lock (_sync)
          _file = value;
      }
    }

    /// <summary>
    /// Gets or sets the current logging level.
    /// </summary>
    /// <remarks>
    /// A log with lower than the value of this property cannot be outputted.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   One of the <see cref="LogLevel"/> enum values.
    ///   </para>
    ///   <para>
    ///   It represents the current logging level.
    ///   </para>
    /// </value>
    public LogLevel Level {
      get {
        return _level;
      }

      set {
        lock (_sync)
          _level = value;
      }
    }

    /// <summary>
    /// Gets or sets the delegate used to output a log.
    /// </summary>
    /// <value>
    ///   <para>
    ///   An <see cref="T:System.Action{LogData, string}"/> delegate.
    ///   </para>
    ///   <para>
    ///   It represents the delegate called when the logger outputs a log.
    ///   </para>
    ///   <para>
    ///   The string parameter passed to the delegate is the value of
    ///   the <see cref="Logger.File"/> property.
    ///   </para>
    ///   <para>
    ///   If the value to set is <see langword="null"/>, the default
    ///   output method is set.
    ///   </para>
    /// </value>
    public Action<LogData, string> Output {
      get {
        return _output;
      }

      set {
        lock (_sync)
          _output = value ?? defaultOutput;
      }
    }

    #endregion

    #region Private Methods

    private static void defaultOutput (LogData data, string path)
    {
      var val = data.ToString ();

      Console.WriteLine (val);

      if (path != null && path.Length > 0)
        writeToFile (val, path);
    }

    private void output (string message, LogLevel level)
    {
      lock (_sync) {
        if (_level > level)
          return;

        try {
          var data = new LogData (level, new StackFrame (2, true), message);

          _output (data, _file);
        }
        catch (Exception ex) {
          var data = new LogData (
                       LogLevel.Fatal,
                       new StackFrame (0, true),
                       ex.Message
                     );

          Console.WriteLine (data.ToString ());
        }
      }
    }

    private static void writeToFile (string value, string path)
    {
      using (var writer = new StreamWriter (path, true))
      using (var syncWriter = TextWriter.Synchronized (writer))
        syncWriter.WriteLine (value);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Outputs the specified message as a log with the Debug level.
    /// </summary>
    /// <remarks>
    /// If the current logging level is higher than the Debug level,
    /// this method does not output the message as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that specifies the message to output.
    /// </param>
    public void Debug (string message)
    {
      if (_level > LogLevel.Debug)
        return;

      output (message, LogLevel.Debug);
    }

    /// <summary>
    /// Outputs the specified message as a log with the Error level.
    /// </summary>
    /// <remarks>
    /// If the current logging level is higher than the Error level,
    /// this method does not output the message as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that specifies the message to output.
    /// </param>
    public void Error (string message)
    {
      if (_level > LogLevel.Error)
        return;

      output (message, LogLevel.Error);
    }

    /// <summary>
    /// Outputs the specified message as a log with the Fatal level.
    /// </summary>
    /// <param name="message">
    /// A <see cref="string"/> that specifies the message to output.
    /// </param>
    public void Fatal (string message)
    {
      if (_level > LogLevel.Fatal)
        return;

      output (message, LogLevel.Fatal);
    }

    /// <summary>
    /// Outputs the specified message as a log with the Info level.
    /// </summary>
    /// <remarks>
    /// If the current logging level is higher than the Info level,
    /// this method does not output the message as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that specifies the message to output.
    /// </param>
    public void Info (string message)
    {
      if (_level > LogLevel.Info)
        return;

      output (message, LogLevel.Info);
    }

    /// <summary>
    /// Outputs the specified message as a log with the Trace level.
    /// </summary>
    /// <remarks>
    /// If the current logging level is higher than the Trace level,
    /// this method does not output the message as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that specifies the message to output.
    /// </param>
    public void Trace (string message)
    {
      if (_level > LogLevel.Trace)
        return;

      output (message, LogLevel.Trace);
    }

    /// <summary>
    /// Outputs the specified message as a log with the Warn level.
    /// </summary>
    /// <remarks>
    /// If the current logging level is higher than the Warn level,
    /// this method does not output the message as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that specifies the message to output.
    /// </param>
    public void Warn (string message)
    {
      if (_level > LogLevel.Warn)
        return;

      output (message, LogLevel.Warn);
    }

    #endregion
  }
}
