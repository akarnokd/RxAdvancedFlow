using RxAdvancedFlow.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.completable
{
    sealed class MergeCompletableSubscriber : ICompletableSubscriber
    {
        readonly ICompletableSubscriber actual;

        readonly SetCompositeDisposable all;

        int wip;

        public MergeCompletableSubscriber(ICompletableSubscriber actual)
        {
            this.actual = actual;
            this.all = new SetCompositeDisposable();
        }

        public void OnComplete()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                all.Dispose();
                actual.OnComplete();
            }
        }

        public void OnError(Exception e)
        {
            if (Interlocked.Exchange(ref wip, 0) > 0)
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

        public void IncrementWip()
        {
            Interlocked.Increment(ref wip);
        }

        public void SpWip(int v)
        {
            wip = v;
        }
        
        public int LvWip()
        {
            return Volatile.Read(ref wip);
        }

        public bool IsDisposed()
        {
            return all.IsDisposed();
        }
    }
}
