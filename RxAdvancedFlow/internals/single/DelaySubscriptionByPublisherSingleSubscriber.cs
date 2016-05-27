using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class DelaySubscriptionByPublisherSingleSubscriber<T, U>
        : ISingleSubscriber<T>, ISubscriber<U>, IDisposable
    {
        readonly ISingle<T> source;

        readonly ISingleSubscriber<T> actual;

        bool once;

        ISubscription other;

        IDisposable d;

        public DelaySubscriptionByPublisherSingleSubscriber(ISingle<T> source, ISingleSubscriber<T> actual)
        {
            this.source = source;
            this.actual = actual;
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
            actual.OnSuccess(t);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(U value)
        {
            if (!once)
            {
                once = true;

                SubscriptionHelper.Terminate(ref other);

                DoSubscribe();
            }
        }

        public void OnComplete()
        {
            if (!once)
            {
                once = true;

                DoSubscribe();
            }
        }

        public void Dispose()
        {
            SubscriptionHelper.Terminate(ref other);
            DisposableHelper.Terminate(ref d);
        }

        void DoSubscribe()
        {
            source.Subscribe(this);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.other, s))
            {
                s.Request(long.MaxValue);
            }
            else
            {
                s.Cancel();
                OnSubscribeHelper.ReportSubscriptionSet();
            }
        }
    }
}
