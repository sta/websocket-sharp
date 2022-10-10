using System.Collections.Generic;

namespace StreamThreads
{
    public class StreamStateSwitch : StreamState
    {
        public IEnumerable<StreamState> OnSwitch;
        internal Predicate Condition;

        public StreamStateSwitch(IEnumerable<StreamState> onSwitch, Predicate condition)
        {
            OnSwitch = onSwitch;
            Condition = condition;
        }

        internal override StateTypes StateType => StateTypes.Switch;
    }
    public class StreamStateSwitch<T> : StreamState<T>
    {
        public IEnumerable<StreamState> OnSwitch;
        internal Predicate Condition;

        public StreamStateSwitch(IEnumerable<StreamState> onSwitch, Predicate condition)
        {
            OnSwitch = onSwitch;
            Condition = condition;
        }

        internal override StateTypes StateType => StateTypes.Switch;
    }

}
