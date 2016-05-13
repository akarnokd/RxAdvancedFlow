using RxAdvancedFlow.internals.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.single
{
    sealed class RetryFiniteSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingle<T> source;

        readonly ISingleSubscriber<T> actual;

        long remaining;

        IDisposable d;

        int wip;

        public RetryFiniteSingleSubscriber(ISingle<T> source, ISingleSubscriber<T> actual, long times)
        {
            this.source = source;
            this.actual = actual;
            this.remaining = times;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void OnSubscribe(IDisposable d)
        {
            DisposableHelper.Replace(ref this.d, d);
        }

        public void OnSuccess(T t)
        {
            actual.OnSuccess(t);
        }

        public void OnError(Exception e)
        {
            Resubscribe();
        }

        internal void Resubscribe()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                do
                {
                    if (--remaining <= 0)
                    {
                        return;
                    }
                    source.Subscribe(this);

                } while (Interlocked.Decrement(ref wip) != 0);
            }
        }
    }
}
