using System;
using System.Threading;

namespace RxAdvancedFlow.internals.single
{
    sealed class UsingSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingleSubscriber<T> actual;

        readonly Action onTerminated;

        readonly bool eager;

        IDisposable d;

        int once;

        public UsingSingleSubscriber(ISingleSubscriber<T> actual, Action onTerminated, bool eager)
        {
            this.actual = actual;
            this.onTerminated = onTerminated;
            this.eager = eager;
        }

        public void OnSubscribe(IDisposable d)
        {
            if (OnSubscribeHelper.SetDisposable(ref this.d, d))
            {
                actual.OnSubscribe(this);
            }
        }

        public void OnSuccess(T t)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) != 0)
            {
                return;
            }

            if (eager)
            {
                
            }

            actual.OnSuccess(t);

            if (!eager)
            {
                DoTerminate();
            }
        }

        public void OnError(Exception e)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) != 0)
            {
                return;
            }

            if (eager)
            {

            }

            actual.OnError(e);

            if (!eager)
            {
                DoTerminate();
            }
        }

        void DoTerminate()
        {
            try
            {
                onTerminated();
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) != 0)
            {
                return;
            }

            d.Dispose();

            DoTerminate();
        }
    }
}
