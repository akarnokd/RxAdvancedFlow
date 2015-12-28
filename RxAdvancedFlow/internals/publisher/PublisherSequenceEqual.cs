using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSequenceEqual<T> : ISubscription
    {
        readonly ISubscriber<bool> actual;

        readonly int bufferSize;

        readonly PublisherSequenceEqualInner firstInner;
        readonly PublisherSequenceEqualInner secondInner;

        readonly IEqualityComparer<T> comparer;

        BasicBackpressureStruct bp;

        ISubscription first;

        ISubscription second;

        int consumed;

        int limit;

        public PublisherSequenceEqual(ISubscriber<bool> actual, int bufferSize, IEqualityComparer<T> comparer)
        {
            this.actual = actual;
            this.bufferSize = bufferSize;
            this.comparer = comparer;
            this.limit = bufferSize - (bufferSize >> 2);
            this.firstInner = new PublisherSequenceEqualInner(this, 0);
            this.secondInner = new PublisherSequenceEqualInner(this, 1);
        }

        public void Subscribe(IPublisher<T> firstSource, IPublisher<T> secondSource)
        {
            firstSource.Subscribe(firstInner);
            secondSource.Subscribe(secondInner);
        }

        public void Cancel()
        {
            bp.Cancel();

            SubscriptionHelper.Terminate(ref first);
            SubscriptionHelper.Terminate(ref second);
        }

        public void Set(int index, ISubscription s)
        {
            if (index == 0)
            {
                SubscriptionHelper.SetOnce(ref first, s);
            }
            else
            {
                SubscriptionHelper.SetOnce(ref second, s);
            }
        }
        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                bp.Request(n);

                Drain();
            }
        }

        internal void Drain()
        {
            if (!bp.Enter())
            {
                return;
            }

            int missed = 1;
            for (;;)
            {
                for (;;)
                {
                    if (bp.IsCancelled())
                    {
                        return;
                    }

                    bool d1 = firstInner.IsDone();

                    T t1;
                    bool e1 = !firstInner.Peek(out t1);

                    bool d2 = secondInner.IsDone();
                    T t2;
                    bool e2 = !secondInner.Peek(out t2);


                    if (d1 && d2 && e1 && e2)
                    {
                        Exception ex1 = firstInner.Error();
                        Exception ex2 = secondInner.Error();

                        if (Equals(ex1, ex2))
                        {
                            if (bp.Requested() != 0L)
                            {
                                actual.OnNext(true);
                                actual.OnComplete();
                                return;
                            }
                        }
                    }
                    else
                    if ((d1 && e1) != (d2 && e2))
                    {
                        if (bp.Requested() != 0L)
                        {
                            actual.OnNext(false);
                            actual.OnComplete();
                            return;
                        }
                    } else
                    if (!e1 && !e2)
                    {
                        if (!comparer.Equals(t1, t2))
                        {
                            if (bp.Requested() != 0)
                            {
                                actual.OnNext(false);
                                actual.OnComplete();
                                return;
                            }
                        } else
                        {
                            firstInner.Poll(out t1);
                            secondInner.Poll(out t2);

                            int c = consumed + 1;

                            if (c >= limit)
                            {
                                consumed = 0;

                                first.Request(c);
                                second.Request(c);
                            } else
                            {
                                consumed = c;
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        sealed class PublisherSequenceEqualInner : ISubscriber<T>
        {
            readonly PublisherSequenceEqual<T> parent;

            readonly int index;

            SpscArrayQueueStruct<T> q;

            Exception error;

            bool done;

            public PublisherSequenceEqualInner(PublisherSequenceEqual<T> parent, int index)
            {
                this.parent = parent;
                this.index = index;
                q.Init(parent.bufferSize);
            }

            internal bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal Exception Error()
            {
                return error;
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                parent.Drain();
            }

            public void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                parent.Drain();
            }

            public void OnNext(T t)
            {
                if (!q.Offer(t))
                {
                    OnError(BackpressureHelper.MissingBackpressureException());
                } else
                {
                    parent.Drain();
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.Set(index, s);
                s.Request(parent.bufferSize);
            }

            internal bool Poll(out T t)
            {
                return q.Poll(out t);
            }

            internal bool Peek(out T t)
            {
                return q.Peek(out t);
            }

            internal bool IsEmpty()
            {
                return q.IsEmpty();
            }
        }
    }
}
