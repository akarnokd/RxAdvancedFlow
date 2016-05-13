using System;
using System.Threading;

namespace RxAdvancedFlow.internals.completable
{
    sealed class UsingCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly bool eager;

        readonly Action disposeState;

        int once;

        IDisposable d;

        public UsingCompletableSubscriber(ICompletableSubscriber actual, bool eager, Action disposeState)
        {
            this.actual = actual;
            this.eager = eager;
            this.disposeState = disposeState;
        }

        public void Dispose()
        {
            d.Dispose();

            Clear();
        }

        void Clear()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                try
                {
                    disposeState();
                }
                catch (Exception ex)
                {
                    RxAdvancedFlowPlugins.OnError(ex);
                }
            }

        }

        public void OnComplete()
        {
            if (eager)
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        disposeState();
                    }
                    catch (Exception ex)
                    {
                        actual.OnError(ex);
                        return;
                    }
                }
            }

            actual.OnComplete();

            if (!eager)
            {
                Clear();
            }
        }

        public void OnError(Exception e)
        {
            if (eager)
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    try
                    {
                        disposeState();
                    }
                    catch (Exception ex)
                    {
                        actual.OnError(new AggregateException(e, ex));
                        return;
                    }
                }
            }

            actual.OnError(e);

            if (!eager)
            {
                Clear();
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            if (OnSubscribeHelper.SetDisposable(ref this.d, d))
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
