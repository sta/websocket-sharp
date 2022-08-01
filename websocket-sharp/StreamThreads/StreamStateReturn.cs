namespace StreamThreads
{
    public interface IStreamStateReturn
    {
        object GetValue();
    }

    public class StreamStateReturn : StreamState, IStreamStateReturn
    {
        internal override StateTypes StateType => StateTypes.Return;

        public object Return;

        public StreamStateReturn()
        {

        }

        public StreamStateReturn(object ret)
        {
            Return = ret;
        }

        public object GetValue()
        {
            return Return;
        }
    }

    public class StreamStateReturn<T> : StreamState<T>, IStreamStateReturn
    {
        internal override StateTypes StateType => StateTypes.Return;

        public T Return;

        public StreamStateReturn()
        {
        }

        public StreamStateReturn(T ret)
        {
            Return = ret;
        }

        public object GetValue()
        {
            return Return;
        }
    }


}
