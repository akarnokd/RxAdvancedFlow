using ReactiveStreamsCS;
using RxAdvancedFlow.disposables;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherDelayFull<T> : ISubscriber<T>, ISubscription
    {
        readonly IWorker worker;

        readonly TimeSpan delay;

        ISubscriber<T> actual;

        ISubscription s;

        public PublisherDelayFull(ISubscriber<T> actual, IWorker worker, TimeSpan delay)
        {
            this.actual = actual;
            this.worker = worker;
        }

        public void Cancel()
        {
            s.Cancel();
            worker.Dispose();
        }

        public void OnComplete()
        {
            worker.Schedule(this.OnCompleteDelayed, delay);
        }

        void OnCompleteDelayed()
        {
            worker.Dispose();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            worker.Schedule(() => actual.OnError(e), delay);
        }

        public void OnNext(T t)
        {
            worker.Schedule(() => actual.OnNext(t), delay);
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
    }

    sealed class PublisherDelayNormal<T> : ISubscriber<T>, ISubscription
    {
        readonly IWorker worker;

        readonly TimeSpan delay;

        LockedSerializedSubscriberStruct<T> actual;

        ISubscription s;

        public PublisherDelayNormal(ISubscriber<T> actual, IWorker worker, TimeSpan delay)
        {
            this.actual.Init(actual);
            this.worker = worker;
        }

        public void Cancel()
        {
            s.Cancel();
            worker.Dispose();
        }

        public void OnComplete()
        {
            worker.Schedule(this.OnCompleteDelayed, delay);
        }

        void OnCompleteDelayed()
        {
            worker.Dispose();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            worker.Dispose();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            worker.Schedule(() => actual.OnNext(t), delay);
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
    }

    sealed class PublisherDelaySelector<T, U> : ISubscriber<T>, ISubscription
    {
        readonly Func<T, IPublisher<U>> delayFactory;

        LockedSerializedSubscriberStruct<T> actual;

        ISubscription s;

        int wip;

        SetCompositeDisposable set;

        bool done;

        public PublisherDelaySelector(ISubscriber<T> actual, Func<T, IPublisher<U>> delayFactory)
        {
            this.actual.Init(actual);
            wip = 1;
            this.delayFactory = delayFactory;
            this.set = new SetCompositeDisposable();
        }

        void SignalNext(PublisherDelaySelectorInner inner, T t)
        {
            set.Remove(inner);

            actual.OnNext(t);

            OnComplete();
        }

        void SignalError(Exception e)
        {
            Cancel();

            actual.OnError(e);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            IPublisher<U> p;

            try
            {
                p = delayFactory(t);
            }
            catch (Exception e)
            {
                done = true;
                Cancel();
                actual.OnError(e);
                return;
            }

            if (p == null)
            {
                done = true;
                Cancel();
                actual.OnError(new NullReferenceException("The delayFactory returned a null Publisher"));
                return;
            }

            var inner = new PublisherDelaySelectorInner(this, t);

            if (set.Add(inner))
            {
                p.Subscribe(inner);
            }
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            Cancel();

            actual.OnError(e);
        }

        public void OnComplete()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                actual.OnComplete();
            }
        }

        public void Request(long n)
        {
            s.Request(n);
        }

        public void Cancel()
        {
            s.Cancel();

            set.Dispose();
        }

        sealed class PublisherDelaySelectorInner : ISubscriber<U>, IDisposable
        {
            readonly PublisherDelaySelector<T, U> parent;

            readonly T value;

            ISubscription s;

            bool done;

            public PublisherDelaySelectorInner(PublisherDelaySelector<T, U> parent, T value)
            {
                this.parent = parent;
                this.value = value;
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }

                parent.SignalNext(this, value);
            }

            public void OnError(Exception e)
            {
                parent.SignalError(e);
            }

            public void OnNext(U t)
            {
                if (done)
                {
                    return;
                }
                done = true;

                parent.SignalNext(this, value);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void Dispose()
            {
                SubscriptionHelper.Terminate(ref s);
            }
        }
    }
}
