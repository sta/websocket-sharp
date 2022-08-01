using System;
using System.Collections.Generic;

namespace StreamThreads
{
    internal class BackgroundState
    {
        internal bool Enabled = true;
        internal bool SwitchState;
        internal Action Lambda;
        internal Predicate Condition;
        internal StreamState BackgroundLoop;
        internal IEnumerator<StreamState> SwitchFunction;
    }

    internal class BackgroundState<T> : BackgroundState
    {
        internal new IEnumerator<StreamState<T>> SwitchFunction;
    }
}