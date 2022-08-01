using System;

namespace StreamThreads
{
    public class StreamStateLambda : StreamState
    {
        internal Action TerminateLambda;
        internal Predicate Lambda;

        public StreamStateLambda(Predicate lambdaloop) : base()
        {
            Lambda = lambdaloop;
            TerminateLambda = null;
        }

        public override bool Loop()
        {
            return Lambda();
        }

        public override void Terminate()
        {
            TerminateLambda.Invoke();
        }
    }
    public class StreamStateLambda<T> : StreamState<T>
    {
        internal Action TerminateLambda;
        internal Predicate Lambda;

        public StreamStateLambda(Predicate lambdaloop) : base()
        {
            Lambda = lambdaloop;
            TerminateLambda = null;
        }

        public override bool Loop()
        {
            return Lambda();
        }

        public override void Terminate()
        {
            TerminateLambda.Invoke();
        }
    }


}
