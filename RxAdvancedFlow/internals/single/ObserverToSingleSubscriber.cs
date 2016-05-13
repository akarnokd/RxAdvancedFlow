using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class ObserverToSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly IObserver<T> actual;

        IDisposable d;

        public ObserverToSingleSubscriber(IObserver<T> actual)
        {
            this.actual = actual;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (!DisposableHelper.SetOnce(ref this.d, d))
            {
                d?.Dispose();
                OnSubscribeHelper.ReportDisposableSet();
            }
        }

        public void OnSuccess(T t)
        {
            actual.OnNext(t);
            actual.OnCompleted();
        }
    }
}
