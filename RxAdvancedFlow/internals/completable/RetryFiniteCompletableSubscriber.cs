using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class RetryFiniteCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly ICompletable source;

        IDisposable d;

        int wip;

        long remaining;

        public RetryFiniteCompletableSubscriber(ICompletableSubscriber actual, 
            ICompletable source, long times)
        {
            this.actual = actual;
            this.source = source;
            this.remaining = times;
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

                    if (--remaining < 0)
                    {
                        return;
                    }

                    source.Subscribe(this);

                } while (Interlocked.Decrement(ref wip) == 0);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            DisposableHelper.Replace(ref this.d, d);
        }
    }
}
