using RxAdvancedFlow.internals.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.completable
{
    sealed class RetryIfCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly ICompletable source;

        readonly Func<Exception, bool> retryIf;

        IDisposable d;

        int wip;

        public RetryIfCompletableSubscriber(ICompletableSubscriber actual, 
            ICompletable source, Func<Exception, bool> retryIf)
        {
            this.actual = actual;
            this.source = source;
            this.retryIf = retryIf;
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
            bool b;

            try
            {
                b = retryIf(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            if (b)
            {
                Resubscribe();
            }
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
