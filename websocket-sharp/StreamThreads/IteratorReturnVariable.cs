using System;

namespace StreamThreads
{
    public enum IteratorStates { Inactive, Running, Ended, Terminated, Faulted }

    public class IteratorReturnVariable
    {
        private object _value;
        private bool _hasvalue = false;

        public IteratorStates IteratorState = IteratorStates.Inactive;

        public object Value
        {
            get
            {
                if (!_hasvalue)
                    throw new Exception("Variable value has not yet been set");

                return _value;
            }

            set
            {
                _value = value;
                _hasvalue = true;
            }
        }
        public bool HasValue()
        {
            return _hasvalue;
        }
        public bool HasEnded => IteratorState == IteratorStates.Ended;
        public bool WasTerminated => IteratorState == IteratorStates.Terminated;
        public bool IsRunning => IteratorState == IteratorStates.Running;
        public bool Faulted => IteratorState == IteratorStates.Faulted;
        public override string ToString()
        {
            return $"{_value}";
        }
        public StreamState Await()
        {
            return new StreamStateLambda(() => !(IteratorState == IteratorStates.Inactive || IteratorState == IteratorStates.Running));
        }
    }

    public class IteratorReturnVariable<T> : IteratorReturnVariable
    {
        public new T Value
        {
            get => (T)base.Value;
            set => base.Value = value;
        }

        public static implicit operator T(IteratorReturnVariable<T> v)
        {
            return v.Value;
        }
    }
}