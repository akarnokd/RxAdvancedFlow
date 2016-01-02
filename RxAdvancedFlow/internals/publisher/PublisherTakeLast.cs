using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherTakeLast<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly int n;

        ISubscription s;

        long requested;

        bool cancelled;

        ArrayQueue<T> queue;

        public PublisherTakeLast(ISubscriber<T> actual, int n)
        {
            this.actual = actual;
            this.n = n;
            this.queue = new ArrayQueue<T>();
        }

        public void Cancel()
        {
            Volatile.Write(ref cancelled, true);
            s.Cancel();
        }

        public void OnComplete()
        {
            BackpressureHelper.PostComplete(ref requested, queue, actual, ref cancelled);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            ArrayQueue<T> q = queue;
            if (q.Size() == n)
            {
                q.Drop();
            }
            q.Offer(t);
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
                BackpressureHelper.PostCompleteRequest(ref requested, n, queue, actual, ref cancelled);
            }
        }
    }

    sealed class PublisherTakeLastOne<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ScalarDelayedSubscriptionStruct<T> sds;

        bool hasValue;

        ISubscription s;

        public PublisherTakeLastOne(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            if (hasValue)
            {
                sds.Complete(sds.Value(), actual);
            }
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (!hasValue)
            {
                hasValue = true;
            }
            sds.SetValue(t);
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
            sds.Request(n, actual);
        }
    }

    sealed class PublisherTakeLastTimed<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IScheduler scheduler;

        readonly ArrayQueue<TimedValue> q;

        readonly long maxAge;

        ISubscription s;

        long requested;

        bool cancelled;

        public PublisherTakeLastTimed(ISubscriber<T> actual, long maxAge, IScheduler scheduler)
        {
            this.actual = actual;
            this.scheduler = scheduler;
            this.maxAge = maxAge;
            this.q = new ArrayQueue<TimedValue>();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void OnNext(T t)
        {
            long now = scheduler.NowUtc();

            Trim(now);

            TimedValue v = new TimedValue();
            v.value = t;
            v.timestamp = now + maxAge;
            q.Offer(v);
        }

        void Trim(long now)
        {
            for (;;)
            {
                TimedValue v;
                if (!q.Peek(out v))
                {
                    break;
                }

                if (v.timestamp < now)
                {
                    q.Drop();
                }
                else
                {
                    break;
                }
            }

        }

        public void OnError(Exception e)
        {
            q.Clear();

            actual.OnError(e);
        }

        public void OnComplete()
        {
            Trim(scheduler.NowUtc());

            BackpressureHelper.PostComplete(ref requested, q, actual, ref cancelled, v => v.value);
        }

        public void Request(long n)
        {
            BackpressureHelper.PostCompleteRequest(ref requested, n, q,
                            actual, ref cancelled, v => v.value);
        }

        public void Cancel()
        {
            Volatile.Write(ref cancelled, true);
        }

        struct TimedValue
        {
            internal T value;
            internal long timestamp;
        }
    }

}
