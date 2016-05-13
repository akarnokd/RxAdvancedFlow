using System;
using RxAdvancedFlow.internals.disposables;

namespace RxAdvancedFlow.internals.completable
{
    sealed class ObserverToCompletableSubscriber<T> : IDisposable, ICompletableSubscriber
    {
        readonly IObserver<T> observer;

        IDisposable s;
        
        public ObserverToCompletableSubscriber(IObserver<T> o)
        {
            this.observer = o;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref s);
        }

        public void OnComplete()
        {
            observer.OnCompleted();
        }

        public void OnError(Exception e)
        {
            observer.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (!DisposableHelper.SetOnce(ref s, d))
            {
                OnSubscribeHelper.ReportDisposableSet();
            }
        }
    }
}
