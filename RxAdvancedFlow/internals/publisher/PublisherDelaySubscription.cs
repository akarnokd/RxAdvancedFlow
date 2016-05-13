using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherDelaySubscription<T, U> : ISubscriber<U>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IPublisher<T> source;

        bool done;

        ISubscription main;

        SingleArbiterStruct arbiter;

        public PublisherDelaySubscription(ISubscriber<T> actual, IPublisher<T> source)
        {
            this.actual = actual;
            this.source = source;
        }

        public void Cancel()
        {
            SubscriptionHelper.Terminate(ref main);
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            SubscribeActual();
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            actual.OnError(e);
        }

        public void OnNext(U t)
        {
            if (done)
            {
                return;
            }
            done = true;

            main.Cancel();

            SubscribeActual();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref main, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        void SubscribeActual()
        {
            source.Subscribe(new PublisherDelaySubscriptionActual(this, actual));
        }

        public void Set(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        sealed class PublisherDelaySubscriptionActual : ISubscriber<T>
        {
            readonly PublisherDelaySubscription<T, U> parent;

            readonly ISubscriber<T> actual;

            public PublisherDelaySubscriptionActual(PublisherDelaySubscription<T, U> parent, ISubscriber<T> actual)
            {
                this.parent = parent;
                this.actual = actual;
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.Set(s);
            }

            public void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnNext(object element)
            {
                throw new NotImplementedException();
            }
        }
    }

    sealed class PublisherDelaySubscriptionTimed<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        IDisposable d;

        SingleArbiterStruct arbiter;

        public PublisherDelaySubscriptionTimed(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        internal void Set(IDisposable d)
        {
            DisposableHelper.Set(ref this.d, d);
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void OnNext(T t)
        {
            actual.OnNext(t);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        public void Cancel()
        {
            arbiter.Cancel();
            DisposableHelper.Terminate(ref d);
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }

}
