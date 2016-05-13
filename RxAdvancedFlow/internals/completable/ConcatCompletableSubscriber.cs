using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RxAdvancedFlow.internals.completable
{
    sealed class ConcatCompletableSubscriber : ICompletableSubscriber, IDisposable
    {

        readonly ICompletableSubscriber actual;

        readonly IEnumerator<ICompletable> it;

        IDisposable d;

        int wip;

        public ConcatCompletableSubscriber(ICompletableSubscriber actual, IEnumerator<ICompletable> it)
        {
            this.actual = actual;
            this.it = it;
        }

        public void OnComplete()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                do
                {
                    if (DisposableHelper.IsTerminated(ref d))
                    {
                        return;
                    }

                    if (it.MoveNext())
                    {
                        ICompletable c = it.Current;

                        c.Subscribe(this);
                    }
                    else
                    {
                        actual.OnComplete();
                    }
                } while (Interlocked.Decrement(ref wip) != 0);
            }
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            DisposableHelper.Replace(ref this.d, d);
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref this.d);
        }
    }
}
