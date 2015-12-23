using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
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
    sealed class PublisherConcatMapSubscriber<T, R> : ISubscriber<T>, ISubscription
    {
        readonly int prefetch;

        readonly Func<T, IPublisher<R>> mapper;

        readonly InnerSubscriber inner;

        LockedSerializedSubscriberStruct<R> actual;

        ISubscription s;

        SpscStructArrayQueue<T> q;

        MultiArbiterStruct arbiter;

        bool active;

        int wip;

        bool done;

        public PublisherConcatMapSubscriber(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
        {
            this.actual.Init(actual);
            this.prefetch = prefetch;
            this.mapper = mapper;
            this.inner = new InnerSubscriber(actual, this);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                if (!arbiter.IsCancelled())
                {
                    s.Request(prefetch);
                }
            }
        }

        public void OnNext(T t)
        {
            if (q.Offer(t))
            {
                Drain();
            }
            else
            {
                s.Cancel();

                arbiter.Cancel();

                actual.OnError(BackpressureHelper.MissingBackpressureException());
            }

        }

        

        public void OnError(Exception e)
        {
            arbiter.Cancel();

            actual.OnError(e);
        }

        public void OnComplete()
        {
            Volatile.Write(ref done, true);

            Drain();
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        public void Cancel()
        {
            actual.Cancel();
            arbiter.Cancel();
        }

        internal void Set(ISubscription s)
        {
            arbiter.Set(s);
        }

        internal void Produced(long n)
        {
            arbiter.Produced(n);
        }

        internal void Complete()
        {
            Volatile.Write(ref active, true);

            Drain();
        }

        void Drain()
        {
            if (Interlocked.Increment(ref wip) != 1)
            {
                return;
            }

            do
            {
                if (!Volatile.Read(ref active))
                {
                    T t;

                    bool d = Volatile.Read(ref done);

                    bool empty = !q.Poll(out t);

                    if (d && empty)
                    {
                        actual.OnComplete();
                        return;
                    }

                    if (!empty)
                    {
                        IPublisher<R> p;

                        try
                        {
                            p = mapper(t);
                        }
                        catch (Exception ex)
                        {
                            s.Cancel();

                            actual.OnError(ex);

                            return;
                        }

                        Volatile.Write(ref active, true);

                        p.Subscribe(inner);

                        if (!Volatile.Read(ref done))
                        {
                            s.Request(1);
                        }
                    }
                }
            } while (Interlocked.Decrement(ref wip) != 0);
        }

        sealed class InnerSubscriber : ISubscriber<R>
        {
            readonly PublisherConcatMapSubscriber<T, R> parent;

            readonly ISubscriber<R> actual;

            public InnerSubscriber(ISubscriber<R> actual, PublisherConcatMapSubscriber<T, R> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.Set(s);
            }

            public void OnNext(R t)
            {
                actual.OnNext(t);

                parent.Produced(1);
            }

            public void OnError(Exception e)
            {

                actual.OnError(e);
            }

            public void OnComplete()
            {
                parent.Complete();
            }
        }
    }
}
