#region License
/*
 * LogData.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013-2022 sta.blockhead
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
using System.Text;

namespace WebSocketSharp
{
  /// <summary>
  /// Represents a log data used by the <see cref="Logger"/> class.
  /// </summary>
  public class LogData
  {
    #region Private Fields

    private StackFrame _caller;
    private DateTime   _date;
    private LogLevel   _level;
    private string     _message;

    #endregion

    #region Internal Constructors

    internal LogData (LogLevel level, StackFrame caller, string message)
    {
      _level = level;
      _caller = caller;
      _message = message ?? String.Empty;

      _date = DateTime.Now;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the information of the logging method caller.
    /// </summary>
    /// <value>
    /// A <see cref="StackFrame"/> that provides the information of
    /// the logging method caller.
    /// </value>
    public StackFrame Caller {
      get {
        return _caller;
      }
    }

    /// <summary>
    /// Gets the date and time when the log data was created.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> that represents the date and time when
    /// the log data was created.
    /// </value>
    public DateTime Date {
      get {
        return _date;
      }
    }

    /// <summary>
    /// Gets the logging level of the log data.
    /// </summary>
    /// <value>
    /// One of the <see cref="LogLevel"/> enum values that represents
    /// the logging level of the log data.
    /// </value>
    public LogLevel Level {
      get {
        return _level;
      }
    }

    /// <summary>
    /// Gets the message of the log data.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the message of the log data.
    /// </value>
    public string Message {
      get {
        return _message;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns a string that represents the current instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current instance.
    /// </returns>
    public override string ToString ()
    {
      var date = String.Format ("[{0}]", _date);
      var level = String.Format ("{0,-5}", _level.ToString ().ToUpper ());

      var method = _caller.GetMethod ();
      var type = method.DeclaringType;
#if DEBUG
      var num = _caller.GetFileLineNumber ();
      var caller = String.Format ("{0}.{1}:{2}", type.Name, method.Name, num);
#else
      var caller = String.Format ("{0}.{1}", type.Name, method.Name);
#endif
      var msgs = _message.Replace ("\r\n", "\n").TrimEnd ('\n').Split ('\n');

      if (msgs.Length <= 1)
        return String.Format ("{0} {1} {2} {3}", date, level, caller, _message);

      var buff = new StringBuilder (64);

      buff.AppendFormat ("{0} {1} {2}\n\n", date, level, caller);

      for (var i = 0; i < msgs.Length; i++)
        buff.AppendFormat ("  {0}\n", msgs[i]);

      return buff.ToString ();
    }

    #endregion
  }
}
