#if UBUNTU
using Notifications;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Example1
{
  internal class Notifier : IDisposable
  {
    private volatile bool              _enabled;
    private Queue<NotificationMessage> _queue;
    private ManualResetEvent           _waitHandle;

    public Notifier ()
    {
      _enabled = true;
      _queue = new Queue<NotificationMessage> ();
      _waitHandle = new ManualResetEvent (false);

      ThreadPool.QueueUserWorkItem (
        state => {
          while (_enabled || Count > 0) {
            Thread.Sleep (500);
            if (Count > 0) {
              var msg = dequeue ();
#if UBUNTU
              var nf = new Notification (msg.Summary, msg.Body, msg.Icon);
              nf.AddHint ("append", "allowed");
              nf.Show ();
#else
              Console.WriteLine (msg);
#endif
            }
          }

          _waitHandle.Set ();
        });
    }

    public int Count {
      get {
        lock (((ICollection) _queue).SyncRoot) {
          return _queue.Count;
        }
      }
    }

    private NotificationMessage dequeue ()
    {
      lock (((ICollection) _queue).SyncRoot) {
        return _queue.Dequeue ();
      }
    }

    public void Close ()
    {
      _enabled = false;
      _waitHandle.WaitOne ();
      _waitHandle.Close ();
    }

    public void Notify (NotificationMessage message)
    {
      lock (((ICollection) _queue).SyncRoot) {
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
