using System;
using System.Collections;
using System.Collections.Generic;

namespace StreamThreads
{

    public class StreamStateAwait : StreamState
    {
        internal override StateTypes StateType => StateTypes.Continue;

        public IteratorReturnVariable ReturnValue;
        internal IEnumerator<StreamState> Iterator;
        internal IEnumerable<StreamState> ErrorHandler;
        internal List<BackgroundState> BackgroundThreads = new List<BackgroundState>();

        public StreamStateAwait(IEnumerable<StreamState> c, IteratorReturnVariable returnvalue) : base()
        {
            Iterator = c.GetEnumerator();
            ErrorHandler = null;
            ReturnValue = returnvalue;

            if (returnvalue != null)
                ReturnValue.IteratorState = IteratorStates.Running;
        }

        public override bool Loop()
        {
            while (true)
            {
                try
                {
                    bool continueonce = false;
                    bool running = true;
                    if (Iterator.Current == null)
                    {
                        running = Iterator.MoveNext();
                        continueonce = running && Iterator.Current.StateType == StateTypes.Continue;
                    }
                    else if (Iterator.Current.Loop())
                    {
                        running = Iterator.MoveNext();
                        continueonce = running && Iterator.Current.StateType == StateTypes.Continue;
                    }

                exitfunction:
                    if (!running)
                    {
                        if (Iterator.Current != null)
                            Iterator.Current.Terminate();

                        foreach (var item in BackgroundThreads)
                        {
                            item.BackgroundLoop.Terminate();
                        }

                        if (ReturnValue != null)
                            ReturnValue.IteratorState = IteratorStates.Ended;
                        return true;
                    }

                    switch (Iterator.Current.StateType)
                    {
                        case StateTypes.Continue:
                            if (continueonce)
                                continue;
                            else
                                break;
                        case StateTypes.Error:
                            ErrorHandler = ((StreamStateError)Iterator.Current).OnError;
                            continue;

                        case StateTypes.Switch:
                            var sm = new BackgroundState()
                            {
                                SwitchState = true,
                                SwitchFunction = ((StreamStateSwitch)Iterator.Current).OnSwitch.GetEnumerator(),
                                Condition = ((StreamStateSwitch)Iterator.Current).Condition
                            };
                            BackgroundThreads.Add(sm);
                            continue;

                        case StateTypes.Background:
                            var bgs = new BackgroundState()
                            {
                                BackgroundLoop = ((StreamStateBackground)Iterator.Current).Background,
                                Lambda = ((StreamStateBackground)Iterator.Current).Lambda
                            };
                            BackgroundThreads.Add(bgs);
                            continue;

                        case StateTypes.Return:
                            if (ReturnValue != null)

                                ReturnValue.Value = ((IStreamStateReturn)Iterator.Current).GetValue();

                            running = false;
                            goto exitfunction;

                        default:
                            break;
                    }

                    for (int i = 0; i < BackgroundThreads.Count; i++)
                    {
                        var item = BackgroundThreads[i];
                        try
                        {
                            if (!item.Enabled) continue;

                            if (item.Condition.Invoke())
                            {
                                item.Lambda.Invoke();

                                if (item.SwitchState)
                                {
                                    Iterator = item.SwitchFunction;
                                    BackgroundThreads.Clear();
                                    ErrorHandler = null;
                                }
                                else if (item.BackgroundLoop != null)
                                {
                                    if (item.BackgroundLoop.Loop())
                                    {
                                        BackgroundThreads.RemoveAt(i);
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            BackgroundThreads.RemoveAt(i--);
                            throw;
                        }
                    }

                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);

                    if (ErrorHandler != null)
                    {
                        Iterator = ErrorHandler.GetEnumerator();
                        BackgroundThreads.Clear();
                        ErrorHandler = null;
                    }
                    else
                    {
                        if (ReturnValue != null)
                            ReturnValue.IteratorState = IteratorStates.Faulted;
                        throw;
                    }
                }
            }

        }
        public override void Terminate()
        {
            foreach (var item in BackgroundThreads)
            {
                item.BackgroundLoop.Terminate();
            }
        }
    }

    public class StreamStateAwait<T> : StreamState<T>
    {
        internal override StateTypes StateType => StateTypes.Continue;

        public IteratorReturnVariable ReturnValue;
        internal IEnumerator<StreamState<T>> Iterator;
        internal IEnumerable<StreamState<T>> ErrorHandler;
        internal List<BackgroundState> BackgroundThreads = new List<BackgroundState>();

        public StreamStateAwait(IEnumerable<StreamState<T>> c, IteratorReturnVariable<T> returnvalue) : base()
        {
            Iterator = c.GetEnumerator();
            ErrorHandler = null;
            ReturnValue = returnvalue;

            if (returnvalue != null)
                ReturnValue.IteratorState = IteratorStates.Running;
        }

        public override bool Loop()
        {
            while (true)
            {
                try
                {
                    bool continueonce = false;
                    bool running = true;
                    if (Iterator.Current == null)
                    {
                        running = Iterator.MoveNext();
                        continueonce = running && Iterator.Current.StateType == StateTypes.Continue;
                    }
                    else if (Iterator.Current.Loop())
                    {
                        running = Iterator.MoveNext();
                        continueonce = running && Iterator.Current.StateType == StateTypes.Continue;
                    }

                exitfunction:
                    if (!running)
                    {
                        Iterator.Current.Terminate();

                        foreach (var item in BackgroundThreads)
                        {
                            item.BackgroundLoop.Terminate();
                        }

                        if (ReturnValue != null)
                            ReturnValue.IteratorState = IteratorStates.Ended;
                        return true;
                    }

                    switch (Iterator.Current.StateType)
                    {
                        case StateTypes.Continue:
                            if (continueonce)
                                continue;
                            else
                                break;
                        case StateTypes.Error:
                            ErrorHandler = ((StreamStateError<T>)Iterator.Current).OnError;
                            continue;

                        case StateTypes.Switch:
                            var sm = new BackgroundState()
                            {
                                SwitchState = true,
                                SwitchFunction = ((StreamStateSwitch<T>)Iterator.Current).OnSwitch.GetEnumerator(),
                                Condition = ((StreamStateSwitch<T>)Iterator.Current).Condition
                            };
                            BackgroundThreads.Add(sm);
                            continue;

                        case StateTypes.Background:
                            var bgs = new BackgroundState()
                            {
                                BackgroundLoop = ((StreamStateBackground<T>)Iterator.Current).Background,
                                Lambda = ((StreamStateBackground<T>)Iterator.Current).Lambda
                            };
                            BackgroundThreads.Add(bgs);
                            continue;

                        case StateTypes.Return:
                            if (ReturnValue != null)

                                ReturnValue.Value = ((IStreamStateReturn)Iterator.Current).GetValue();

                            running = false;
                            goto exitfunction;

                        default:
                            break;
                    }

                    for (int i = 0; i < BackgroundThreads.Count; i++)
                    {
                        var item = BackgroundThreads[i];
                        try
                        {
                            if (!item.Enabled) continue;

                            if (item.Condition.Invoke())
                            {
                                item.Lambda.Invoke();

                                if (item.SwitchState)
                                {
                                    Iterator = (IEnumerator<StreamState<T>>)item.SwitchFunction;
                                    BackgroundThreads.Clear();
                                    ErrorHandler = null;
                                }
                                else if (item.BackgroundLoop != null)
                                {
                                    if (item.BackgroundLoop.Loop())
                                    {
                                        BackgroundThreads.RemoveAt(i);
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            BackgroundThreads.RemoveAt(i--);
                            throw;
                        }
                    }

                    return false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);

                    if (ErrorHandler != null)
                    {
                        Iterator = ErrorHandler.GetEnumerator();
                        BackgroundThreads.Clear();
                        ErrorHandler = null;
                    }
                    else
                    {
                        if (ReturnValue != null)
                            ReturnValue.IteratorState = IteratorStates.Faulted;
                        throw;
                    }
                }
            }

        }
        public override void Terminate()
        {
            foreach (var item in BackgroundThreads)
            {
                item.BackgroundLoop.Terminate();
            }
        }

    }

}
