using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherOnBackpressureError<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        bool done;

        PaddedLong requested;

        PaddedLong produced;

        public PublisherOnBackpressureError(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                return;
            }
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            long e = produced.Get();
            long r = requested.GetVolatile();
            if (r != e)
            {
                produced.Set(e + 1);

                actual.OnNext(t);
            }
            else
            {
                done = true;
                s.Cancel();

                actual.OnError(BackpressureHelper.MissingBackpressureException());
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                requested.AddCap(n);
            }
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
