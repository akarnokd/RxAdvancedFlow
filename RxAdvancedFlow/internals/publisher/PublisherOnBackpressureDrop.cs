using Reactive.Streams;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherOnBackpressureDrop<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Action<T> onDrop;

        long requested;

        ISubscription s;

        bool done;

        public PublisherOnBackpressureDrop(ISubscriber<T> actual, Action<T> onDrop)
        {
            this.actual = actual;
            this.onDrop = onDrop;
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
                RxAdvancedFlowPlugins.OnError(e);
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
            long r = Volatile.Read(ref requested);
            if (r != 0L)
            {
                actual.OnNext(t);
                if (r != long.MaxValue)
                {
                    Interlocked.Decrement(ref requested);
                }
            }
            else
            {
                try
                {
                    onDrop?.Invoke(t);
                }
                catch (Exception e)
                {
                    done = true;
                    s.Cancel();

                    actual.OnError(e);
                }
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
                BackpressureHelper.Add(ref requested, n);
            }
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
