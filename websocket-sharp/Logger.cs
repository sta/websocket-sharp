#region License
/*
 * Logger.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013 sta.blockhead
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
  /// Provides the simple logging functions.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   The Logger class provides some methods that output the logs associated with the each
  ///   <see cref="LogLevel"/> values.
  ///   If the <see cref="LogLevel"/> value associated with a log is less than the <see cref="Level"/>,
  ///   the log can not be outputted.
  ///   </para>
  ///   <para>
  ///   The default output action used by the output methods outputs the log data to the standard output stream
  ///   and writes the same log data to the <see cref="Logger.File"/> if it has a valid path.
  ///   </para>
  ///   <para>
  ///   If you want to run custom output action, you can replace the current output action with
  ///   your output action by using the <see cref="SetOutput"/> method.
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
    /// This constructor initializes the current logging level with the <see cref="LogLevel.ERROR"/> and
    /// initializes the path to the log file with <see langword="null"/>.
    /// </remarks>
    public Logger ()
      : this (LogLevel.ERROR, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class
    /// with the specified logging <paramref name="level"/>.
    /// </summary>
    /// <remarks>
    /// This constructor initializes the path to the log file with <see langword="null"/>.
    /// </remarks>
    /// <param name="level">
    /// One of the <see cref="LogLevel"/> values to initialize.
    /// </param>
    public Logger (LogLevel level)
      : this (level, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class
    /// with the specified logging <paramref name="level"/>, path to the log <paramref name="file"/>
    /// and <paramref name="output"/> action.
    /// </summary>
    /// <param name="level">
    /// One of the <see cref="LogLevel"/> values to initialize.
    /// </param>
    /// <param name="file">
    /// A <see cref="string"/> that contains a path to the log file to initialize.
    /// </param>
    /// <param name="output">
    /// An <c>Action&lt;LogData, string&gt;</c> delegate that references the method(s) to initialize.
    /// A <see cref="string"/> parameter to pass to the method(s) is the value of the <see cref="Logger.File"/> 
    /// if any.
    /// </param>
    public Logger (LogLevel level, string file, Action<LogData, string> output)
    {
      _level = level;
      _file = file;
      _output = output != null ? output : defaultOutput;
      _sync = new object ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the path to the log file.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains a path to the log file if any.
    /// </value>
    public string File {
      get {
        return _file;
      }

      set {
        lock (_sync)
        {
          _file = value;
          Warn (String.Format ("The current path to the log file has been changed to {0}.", _file ?? ""));
        }
      }
    }

    /// <summary>
    /// Gets or sets the current logging level.
    /// </summary>
    /// <remarks>
    /// A log associated with a less than the current logging level can not be outputted.
    /// </remarks>
    /// <value>
    /// One of the <see cref="LogLevel"/> values that indicates the current logging level.
    /// </value>
    public LogLevel Level {
      get {
        return _level;
      }

      set {
        _level = value;
        Warn (String.Format ("The current logging level has been changed to {0}.", _level));
      }
    }

    #endregion

    #region Private Methods

    private static void defaultOutput (LogData data, string path)
    {
      var log = data.ToString ();
      Console.WriteLine (log);
      if (path != null && path.Length > 0)
        writeLine (log, path);
    }

    private void output (string message, LogLevel level)
    {
      if (level < _level || message == null || message.Length == 0)
        return;

      lock (_sync)
      {
        LogData data = null;
        try {
          data = new LogData (level, new StackFrame (2, true), message);
          _output (data, _file);
        }
        catch (Exception ex) {
          data = new LogData (LogLevel.FATAL, new StackFrame (0, true), ex.Message);
          Console.WriteLine (data.ToString ());
        }
      }
    }

    private static void writeLine (string value, string path)
    {
      using (var writer = new StreamWriter (path, true))
      using (var syncWriter = TextWriter.Synchronized (writer))
      {
        syncWriter.WriteLine (value);
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Outputs the specified <see cref="string"/> as a log with the <see cref="LogLevel.DEBUG"/>.
    /// </summary>
    /// <remarks>
    /// If the current logging level is greater than the <see cref="LogLevel.DEBUG"/>,
    /// this method does not output <paramref name="message"/> as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to output as a log.
    /// </param>
    public void Debug (string message)
    {
      output (message, LogLevel.DEBUG);
    }

    /// <summary>
    /// Outputs the specified <see cref="string"/> as a log with the <see cref="LogLevel.ERROR"/>.
    /// </summary>
    /// <remarks>
    /// If the current logging level is greater than the <see cref="LogLevel.ERROR"/>,
    /// this method does not output <paramref name="message"/> as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to output as a log.
    /// </param>
    public void Error (string message)
    {
      output (message, LogLevel.ERROR);
    }

    /// <summary>
    /// Outputs the specified <see cref="string"/> as a log with the <see cref="LogLevel.FATAL"/>.
    /// </summary>
    /// <remarks>
    /// If the current logging level is greater than the <see cref="LogLevel.FATAL"/>,
    /// this method does not output <paramref name="message"/> as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to output as a log.
    /// </param>
    public void Fatal (string message)
    {
      output (message, LogLevel.FATAL);
    }

    /// <summary>
    /// Outputs the specified <see cref="string"/> as a log with the <see cref="LogLevel.INFO"/>.
    /// </summary>
    /// <remarks>
    /// If the current logging level is greater than the <see cref="LogLevel.INFO"/>,
    /// this method does not output <paramref name="message"/> as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to output as a log.
    /// </param>
    public void Info (string message)
    {
      output (message, LogLevel.INFO);
    }

    /// <summary>
    /// Replaces the current output action with the specified <paramref name="output"/> action.
    /// </summary>
    /// <remarks>
    /// If <paramref name="output"/> is <see langword="null"/>,
    /// this method replaces the current output action with the default output action.
    /// </remarks>
    /// <param name="output">
    /// An <c>Action&lt;LogData, string&gt;</c> delegate that references the method(s) to set.
    /// A <see cref="string"/> parameter to pass to the method(s) is the value of the <see cref="Logger.File"/>
    /// if any.
    /// </param>
    public void SetOutput (Action<LogData, string> output)
    {
      lock (_sync)
      {
        _output = output != null ? output : defaultOutput;
        Warn ("The current output action has been replaced.");
      }
    }

    /// <summary>
    /// Outputs the specified <see cref="string"/> as a log with the <see cref="LogLevel.TRACE"/>.
    /// </summary>
    /// <remarks>
    /// If the current logging level is greater than the <see cref="LogLevel.TRACE"/>,
    /// this method does not output <paramref name="message"/> as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to output as a log.
    /// </param>
    public void Trace (string message)
    {
      output (message, LogLevel.TRACE);
    }

    /// <summary>
    /// Outputs the specified <see cref="string"/> as a log with the <see cref="LogLevel.WARN"/>.
    /// </summary>
    /// <remarks>
    /// If the current logging level is greater than the <see cref="LogLevel.WARN"/>,
    /// this method does not output <paramref name="message"/> as a log.
    /// </remarks>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to output as a log.
    /// </param>
    public void Warn (string message)
    {
      output (message, LogLevel.WARN);
    }

    #endregion
  }
}
