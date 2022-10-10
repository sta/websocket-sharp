namespace StreamThreads
{
    public delegate bool Predicate();
    internal enum StateTypes { Normal, Background, Error, Switch, Return,
        Continue
    }

    public class StreamState
    {
        public virtual bool Loop() => true;

        public virtual void Terminate()
        {

        }

        internal virtual StateTypes StateType => StateTypes.Normal;
    }

    public class StreamState<T> : StreamState
    {

    }
}
