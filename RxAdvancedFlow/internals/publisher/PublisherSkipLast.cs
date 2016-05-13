using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscribers;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSkipLast<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly int n;

        ArrayQueue<T> queue;

        ISubscription s;

        public PublisherSkipLast(ISubscriber<T> actual, int n)
        {
            this.actual = actual;
            this.queue = new ArrayQueue<T>();
            this.n = n;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        public void OnNext(T t)
        {
            ArrayQueue<T> q = queue;
            if (q.Size() == n)
            {
                T t1;
                q.Poll(out t1);

                actual.OnNext(t);
            }
            q.Offer(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(n);
            }
        }

        public void Request(long n)
        {
            s.Request(n);
        }
    }

    sealed class PublisherSkipLastTimed<T> : ISubscriber<T>, ISubscription
    {
        HalfSerializedSubscriberStruct<T> actual;

        readonly IScheduler scheduler;

        readonly TimeSpan time;

        ISubscription s;

        ArrayQueue<TimedValue> queue;

        public PublisherSkipLastTimed(ISubscriber<T> actual, TimeSpan time, IScheduler scheduler)
        {
            this.actual.Init(actual);
            this.scheduler = scheduler;
            this.time = time;
            this.queue = new ArrayQueue<TimedValue>();
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
            long now = scheduler.NowUtc();

            TimedValue tv = new TimedValue();
            tv.timestamp = now;
            tv.value = t;

            queue.Offer(tv);

            drainOld(now);
        }

        public void OnError(Exception e)
        {
            queue.Clear();
            actual.OnError(e);
        }

        public void OnComplete()
        {
            drainOld(scheduler.NowUtc());
            queue.Clear();
            actual.OnComplete();
        }

        void drainOld(long now)
        {
            now -= (long)time.TotalMilliseconds;

            TimedValue tv;

            for (;;)
            {
                if (!queue.Peek(out tv))
                {
                    break;
                }

                if (tv.timestamp < now)
                {
                    queue.Poll(out tv);
                    actual.OnNext(tv.value);
                }
            }
        }

        public void Request(long n)
        {
            s.Request(n);
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        struct TimedValue
        {
            internal T value;
            internal long timestamp;
        }
    }

}
