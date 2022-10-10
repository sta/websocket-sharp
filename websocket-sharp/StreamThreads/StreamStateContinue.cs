namespace StreamThreads
{
    internal class StreamStateContinue<T> : StreamState<T>
    {
        internal override StateTypes StateType => StateTypes.Continue;
        public override bool Loop()
        {
            return true;
        }
    }

    internal class StreamStateContinue : StreamState
    {
        internal override StateTypes StateType => StateTypes.Continue;
        public override bool Loop()
        {
            return true;
        }
    }
}