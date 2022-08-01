using System;
using System.Collections.Generic;

namespace StreamThreads
{
    public static class StreamExtensions
    {

        public static readonly StreamState OK = new StreamState();
        public static readonly StreamState WaitForever = new StreamStateWaitForever();

        [ThreadStatic]
        private static DateTime _lastrun;
        public static double SecondsSinceLast
        {
            get
            {
                var now = DateTime.Now;
                return (now - _lastrun).TotalSeconds;
            }
            set
            {
                _lastrun = DateTime.Now - TimeSpan.FromSeconds(value);
            }
        }

        public static IEnumerable<StreamState> Until(this IEnumerable<StreamState> me, Predicate condition)
        {
            if (condition()) yield break;

            foreach (var item in me)
            {
                yield return item;

                if (condition()) yield break;
            }
        }
        public static IEnumerable<StreamState> While(this IEnumerable<StreamState> me, Predicate condition)
        {
            var itr = me.GetEnumerator();
            while (true)
            {
                if (!condition())
                    yield return OK;
                else
                {
                    if (!itr.MoveNext())
                        yield break;
                    else
                        yield return itr.Current;
                }
            }
        }
        public static IEnumerable<StreamState> ExitOnError(this IEnumerable<StreamState> me)
        {
            var itr = me.GetEnumerator();
            while (true)
            {
                try
                {
                    if (!itr.MoveNext()) yield break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    yield break;
                }

                yield return itr.Current;

            }
        }
        public static IEnumerable<StreamState> ResumeOnError(this IEnumerable<StreamState> me)
        {
            var itr = me.GetEnumerator();
            while (true)
            {
                try
                {
                    if (!itr.MoveNext()) yield break;
                }
                catch (Exception) { }

                yield return itr.Current;
            }
        }
        public static IEnumerable<StreamState> RestartOnError(this IEnumerable<StreamState> me)
        {
            int maxretries = 1;
            var itr = me.GetEnumerator();
            while (true)
            {
                try
                {
                    if (!itr.MoveNext()) yield break;
                    maxretries = 1;

                }
                catch (Exception)
                {
                    if (--maxretries < 0)
                        throw;

                    itr = me.GetEnumerator();
                }

                if (itr.Current != null)
                    yield return itr.Current;

            }
        }
        public static StreamState Await(this IEnumerable<StreamState> c)
        {
            return new StreamStateAwait(c, null);
        }
        public static StreamState Await(this IEnumerable<StreamState> c, out IteratorReturnVariable returnvalue)
        {
            return new StreamStateAwait(c, returnvalue = new IteratorReturnVariable());
        }
        public static StreamStateAwait<T> Await<T>(this IEnumerable<StreamState<T>> c, out IteratorReturnVariable<T> returnvalue)
        {
            return new StreamStateAwait<T>(c, returnvalue = new IteratorReturnVariable<T>());
        }
        public static StreamState Background(this IEnumerable<StreamState> c)
        {
            return c.Background(out var notused);
        }
        public static StreamState Background(this IEnumerable<StreamState> c, out IteratorReturnVariable returnvalue)
        {
            return new StreamStateBackground(c.Await(out returnvalue));
        }
        public static StreamState Background(Action lambda)
        {
            return new StreamStateBackground(lambda);
        }
        public static StreamStateBackground Background<T>(this IEnumerable<StreamState<T>> c, out IteratorReturnVariable<T> returnvalue)
        {
            return new StreamStateBackground(c.Await<T>(out returnvalue));
        }
        public static StreamState OnError(this IEnumerable<StreamState> c)
        {
            return new StreamStateError(c);
        }
        public static StreamState SwitchOnCondition(this IEnumerable<StreamState> c, Predicate condition)
        {
            return new StreamStateSwitch(c, condition);
        }
        public static StreamStateReturn Return(object returnvalue)
        {
            return new StreamStateReturn(returnvalue);
        }
        public static StreamStateReturn<T> Return<T>(T returnvalue)
        {
            return new StreamStateReturn<T>(returnvalue);
        }
        public static StreamState Sleep(int millis)
        {
            var t = DateTime.Now + TimeSpan.FromMilliseconds(millis);

            return new StreamStateLambda(() => DateTime.Now > t);
        }
        public static StreamState WaitFor(Predicate trigger)
        {
            if (trigger())
                return new StreamStateContinue();
            else
                return new StreamStateLambda(trigger);
        }

        public static StreamState<T> WaitFor<T>(Predicate trigger)
        {
            if (trigger())
                return new StreamStateContinue<T>();
            else
                return new StreamStateLambda<T>(trigger);
        }

        public static void SimulatedError(double probability = 0.1)
        {
            if (new Random().NextDouble() > 1 - probability) throw new Exception("Simulated Error");
        }
    }
}