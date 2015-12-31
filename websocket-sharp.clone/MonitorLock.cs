namespace WebSocketSharp
{
    using System;
    using System.Threading;

    internal class MonitorLock : IDisposable
    {
        private readonly object _lockObject;
        private volatile bool _locked;

        public MonitorLock(object lockObject)
        {
            _lockObject = lockObject;
            lock (_lockObject)
            {
                while (_locked)
                {
                    Monitor.Wait(_lockObject);
                }
                _locked = true;
            }
        }
        public void Dispose()
        {
            lock (_lockObject)
            {
                _locked = false;
                Monitor.Pulse(_lockObject);
            }
        }
    }
}