using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.subscriptions;
using System;

namespace RxAdvancedFlow.internals
{
    public sealed class ScalarSource<T> : ISingle<T>, IPublisher<T>, IObservable<T> 
    {
        readonly T value;

        public ScalarSource(T value)
        {
            this.value = value;
        }

        public T Get()
        {
            return value;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(value);
            observer.OnCompleted();

            return EmptyDisposable.Instance;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            s.OnSubscribe(new ScalarSubscription<T>(value, s));
        }

        public void Subscribe(ISingleSubscriber<T> s)
        {
            s.OnSubscribe(EmptyDisposable.Instance);
            s.OnSuccess(value);
        }
    }
}
