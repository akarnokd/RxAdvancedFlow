using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSampleMain<T> : ISubscriber<T>, ISubscription
    {
        HalfSerializedSubscriberStruct<T> actual;

        IDisposable sampler;

        ISubscription s;

        long requested;

        RefValue<T> value;

        public PublisherSampleMain(ISubscriber<T> actual)
        {
            this.actual.Init(actual);
        }

        void CancelSampler()
        {
            DisposableHelper.Terminate(ref sampler);
        }

        internal void Set(IDisposable sampler)
        {
            DisposableHelper.Set(ref this.sampler, sampler);
        }

        internal void CancelMain()
        {
            SubscriptionHelper.Terminate(ref s);
        }

        public void Cancel()
        {
            CancelSampler();

            CancelMain();
        }

        public void OnComplete()
        {
            CancelSampler();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            CancelSampler();

            actual.OnError(e);
        }

        internal void OtherComplete()
        {
            CancelMain();

            actual.OnComplete();
        }

        internal void OtherError(Exception e)
        {
            CancelMain();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            Volatile.Write(ref value, new RefValue<T>(t));
        }

        internal void Run()
        {
            var v = Interlocked.Exchange(ref value, null);

            if (v != null)
            {
                long r = Volatile.Read(ref requested);

                if (r != 0L)
                {
                    actual.OnNext(v.value);
                    if (r != long.MaxValue)
                    {
                        Interlocked.Decrement(ref requested);
                    }
                }
                else
                {
                    Cancel();

                    actual.OnError(BackpressureHelper.MissingBackpressureException());
                }
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.Add(ref requested, n);
            }
        }
    }

    sealed class RefValue<T>
    {
        internal readonly T value;

        public RefValue(T value)
        {
            this.value = value;
        }
    }

    sealed class PublisherSampleOther<T, U> : ISubscriber<U>, IDisposable
    {
        readonly PublisherSampleMain<T> actual;

        ISubscription s;

        public PublisherSampleOther(PublisherSampleMain<T> actual)
        {
            this.actual = actual;
        }

        public void Dispose()
        {
            SubscriptionHelper.Terminate(ref s);
        }

        public void OnComplete()
        {
            actual.OtherComplete();
        }

        public void OnError(Exception e)
        {
            actual.OtherError(e);
        }

        public void OnNext(U t)
        {
            actual.Run();
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
