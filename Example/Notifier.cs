#if UBUNTU
using Notifications;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Example
{
  internal class Notifier : IDisposable
  {
    private volatile bool              _enabled;
    private ManualResetEvent           _exited;
    private Queue<NotificationMessage> _queue;
    private object                     _sync;

    public Notifier ()
    {
      _enabled = true;
      _exited = new ManualResetEvent (false);
      _queue = new Queue<NotificationMessage> ();
      _sync = ((ICollection) _queue).SyncRoot;

      ThreadPool.QueueUserWorkItem (
        state => {
          while (_enabled || Count > 0) {
            var msg = dequeue ();
            if (msg != null) {
#if UBUNTU
              var nf = new Notification (msg.Summary, msg.Body, msg.Icon);
              nf.AddHint ("append", "allowed");
              nf.Show ();
#else
              Console.WriteLine (msg);
#endif
            }
            else {
              Thread.Sleep (500);
            }
          }

          _exited.Set ();
        }
      );
    }

    public int Count {
      get {
        lock (_sync)
          return _queue.Count;
      }
    }

    private NotificationMessage dequeue ()
    {
      lock (_sync)
        return _queue.Count > 0 ? _queue.Dequeue () : null;
    }

    public void Close ()
    {
      _enabled = false;
      _exited.WaitOne ();
      _exited.Close ();
    }

    public void Notify (NotificationMessage message)
    {
      lock (_sync) {
        if (_enabled)
          _queue.Enqueue (message);
      }
    }

    void IDisposable.Dispose ()
    {
      Close ();
    }
  }
}
