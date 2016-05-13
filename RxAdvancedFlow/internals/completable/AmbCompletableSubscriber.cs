using RxAdvancedFlow.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.completable
{
    sealed class AmbCompletableSubscriber : ICompletableSubscriber
    {
        readonly ICompletableSubscriber actual;

        readonly SetCompositeDisposable all;

        int once;

        public AmbCompletableSubscriber(ICompletableSubscriber actual)
        {
            this.actual = actual;
            this.all = new SetCompositeDisposable();
        }

        public void OnComplete()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                all.Dispose();
                actual.OnComplete();
            }
        }

        public void OnError(Exception e)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                all.Dispose();
                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            all.Add(d);
        }

        public bool IsDisposed()
        {
            return all.IsDisposed();
        }
    }
}
