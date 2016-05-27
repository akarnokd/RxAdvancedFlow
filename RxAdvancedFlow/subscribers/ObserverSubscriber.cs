using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;
using System;

namespace RxAdvancedFlow.subscribers
{
    /// <summary>
    /// An ISubscriber that wraps an IObserver thus making sure an IObserver
    /// can subscribe to an IPublisher and implements IDisposable to allow
    /// external cancellation.
    /// </summary>
    /// <typeparam name="T">The value type observed.</typeparam>
    public sealed class ObserverSubscriber<T> : ISubscriber<T>, IDisposable
    {
        readonly IObserver<T> actual;

        ISubscription s;

        public ObserverSubscriber(IObserver<T> actual)
        {
            this.actual = actual;
        }

        public void Dispose()
        {
            SubscriptionHelper.Terminate(ref s);
        }

        public void OnComplete()
        {
            actual.OnCompleted();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                s.Request(long.MaxValue);
            }
        }
    }
}
