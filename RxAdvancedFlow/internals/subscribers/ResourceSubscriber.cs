using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.subscribers
{
    sealed class ResourceSubscriber<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        IDisposable resource;

        public ResourceSubscriber(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        internal void Set(IDisposable d)
        {
            DisposableHelper.Set(ref this.resource, d);
        }

        public void OnComplete()
        {
            DisposableHelper.Terminate(ref resource);

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            DisposableHelper.Terminate(ref resource);

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            s.Request(n);
        }

        public void Cancel()
        {
            DisposableHelper.Terminate(ref resource);
            s.Cancel();
        }
    }
}
