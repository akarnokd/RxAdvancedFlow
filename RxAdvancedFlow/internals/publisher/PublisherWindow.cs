using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.processors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherWindowExact<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<IPublisher<T>> actual;

        readonly int size;

        ISubscription s;

        IProcessor<T, T> window;

        int wip;

        int once;

        int produced;

        public PublisherWindowExact(ISubscriber<IPublisher<T>> actual, int size)
        {
            this.actual = actual;
            this.size = size;
            this.wip = 1;
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
            var w = window;
            if (w == null)
            {
                Interlocked.Increment(ref wip);

                w = new UnicastProcessor<T>(size, this.InnerDone);
                window = w;

                actual.OnNext(w);
            }

            w.OnNext(t);

            int p = produced + 1;
            if (p == size)
            {
                w.OnComplete();
                window = null;
                produced = 0;
            }
            else
            {
                produced = p;
            }

        }

        public void OnError(Exception e)
        {
            var w = window;
            window = null;

            w?.OnError(e);
            actual.OnError(e);
        }

        public void OnComplete()
        {
            var w = window;
            window = null;

            w?.OnComplete();
            actual.OnComplete();
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                long u = BackpressureHelper.MultiplyCap(n, size);
                s.Request(u);
            }
        }

        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                InnerDone();
            }
        }

        void InnerDone()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                s.Cancel();
            }
        }
    }


    sealed class PublisherWindowSkip<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<IPublisher<T>> actual;

        readonly int size;

        readonly int skip;

        ISubscription s;

        IProcessor<T, T> window;

        int wip;

        int once;

        int produced;

        int requestOnce;

        int index;


        public PublisherWindowSkip(ISubscriber<IPublisher<T>> actual, int size, int skip)
        {
            this.actual = actual;
            this.size = size;
            this.skip = skip;
            this.wip = 1;
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
            var w = window;
            int i = index;
            if (i == 0)
            {
                Interlocked.Increment(ref wip);

                w = new UnicastProcessor<T>(size, this.InnerDone);
                window = w;

                actual.OnNext(w);
            }
            i++;
            if (i == skip)
            {
                index = 0;
            }
            else
            {
                index = i;
            }

            if (w != null)
            {
                w.OnNext(t);

                int p = produced + 1;
                if (p == size)
                {
                    window = null;
                    produced = 0;

                    w.OnComplete();
                }
                else
                {
                    produced = p;
                }
            }

        }

        public void OnError(Exception e)
        {
            var w = window;
            window = null;

            w?.OnError(e);
            actual.OnError(e);
        }

        public void OnComplete()
        {
            var w = window;
            window = null;

            w?.OnComplete();
            actual.OnComplete();
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (Volatile.Read(ref requestOnce) == 0
                    && Interlocked.CompareExchange(ref requestOnce, 1, 0) == 0)
                {
                    long u = BackpressureHelper.MultiplyCap(n, size);
                    long v = BackpressureHelper.MultiplyCap(skip - size, n - 1);
                    long w = BackpressureHelper.AddCap(u, v);
                    s.Request(w);
                }
                else
                {
                    long u = BackpressureHelper.MultiplyCap(n, skip);
                    s.Request(u);
                }
            }
        }

        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                InnerDone();
            }
        }

        void InnerDone()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                s.Cancel();
            }
        }
    }


    sealed class PublisherWindowOverlap<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<IPublisher<T>> actual;

        readonly int size;

        readonly int skip;

        ISubscription s;

        ArrayQueue<IProcessor<T, T>> q;

        int wip;

        int once;

        int produced;

        int requestOnce;

        int index;

        long requested;

        bool cancelled;

        public PublisherWindowOverlap(ISubscriber<IPublisher<T>> actual, int size, int skip)
        {
            this.actual = actual;
            this.size = size;
            this.skip = skip;
            this.wip = 1;
            this.q = new ArrayQueue<IProcessor<T, T>>();
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
            int i = index;
            if (i == 0)
            {
                Interlocked.Increment(ref wip);

                var b = new UnicastProcessor<T>(size, this.InnerDone);

                q.Offer(b);

                actual.OnNext(b);
            }
            i++;
            if (i == skip)
            {
                index = 0;
            }
            else
            {
                index = i;
            }

            q.ForEach(b => b.OnNext(t));

            int p = produced + 1;
            if (p == size)
            {
                IProcessor<T, T> b;
                q.Poll(out b);

                b.OnComplete();

                produced = size - skip;
            }
            else
            {
                produced = p;
            }

        }

        public void OnError(Exception e)
        {
            q.ForEach(v => v.OnError(e));

            q.Clear();

            actual.OnError(e);
        }

        public void OnComplete()
        {
            q.ForEach(e => e.OnComplete());

            q.Clear();

            actual.OnComplete();
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (Volatile.Read(ref requestOnce) == 0
                    && Interlocked.CompareExchange(ref requestOnce, 1, 0) == 0)
                {
                    long u = BackpressureHelper.MultiplyCap(skip, n - 1);
                    long v = BackpressureHelper.AddCap(u, n);
                    s.Request(v);
                }
                else
                {
                    long u = BackpressureHelper.MultiplyCap(skip, n);
                    s.Request(u);
                }
            }
        }

        public void Cancel()
        {

            Volatile.Write(ref cancelled, true);
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                InnerDone();
            }
        }

        void InnerDone()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                s.Cancel();
            }
        }
    }

}
