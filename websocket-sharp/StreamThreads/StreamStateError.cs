using System.Collections.Generic;

namespace StreamThreads
{
    public class StreamStateError : StreamState
    {
        public IEnumerable<StreamState> OnError;

        public StreamStateError(IEnumerable<StreamState> onError)
        {
            OnError = onError;
        }

        internal override StateTypes StateType => StateTypes.Error;
    }

    public class StreamStateError<T> : StreamState<T>
    {
        public IEnumerable<StreamState<T>> OnError;

        public StreamStateError(IEnumerable<StreamState<T>> onError)
        {
            OnError = onError;
        }

        internal override StateTypes StateType => StateTypes.Error;
    }

}
