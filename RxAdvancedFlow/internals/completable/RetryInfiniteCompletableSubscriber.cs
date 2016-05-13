using RxAdvancedFlow.internals.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.completable
{
    sealed class RetryInfiniteCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly ICompletable source;

        IDisposable d;

        int wip;

        public RetryInfiniteCompletableSubscriber(ICompletableSubscriber actual, ICompletable source)
        {
            this.actual = actual;
            this.source = source;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void OnComplete()
        {
            actual.OnComplete();
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
                    if (DisposableHelper.IsTerminated(ref d))
                    {
                        return;
                    }

                    source.Subscribe(this);

                } while (Interlocked.Decrement(ref wip) != 0);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            DisposableHelper.Replace(ref this.d, d);
        }
    }
}
