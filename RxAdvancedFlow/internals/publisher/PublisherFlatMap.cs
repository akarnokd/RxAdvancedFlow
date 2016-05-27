using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    sealed class PublisherFlatMap<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly int maxConcurrency;

        readonly bool delayError;

        readonly int bufferSize;

        readonly int limit;

        readonly Func<T, IPublisher<R>> mapper;

        // source thread

        ISubscription s;

        long uniqueId;

        bool done;

        SpscLinkedArrayQueueStruct<R> q;

        // all kinds of threads

        static readonly PublisherMergeInner[] Empty = new PublisherMergeInner[0];
        static readonly PublisherMergeInner[] Terminated = new PublisherMergeInner[0];

        PublisherMergeInner[] subscribers;

        Exception error;
        static readonly Exception TerminalException = new Exception();

        BasicBackpressureStruct bp;

        // drain loop

        int lastIndex;

        int produced;

        public PublisherFlatMap(ISubscriber<R> actual, int maxConcurrency, bool delayError, int bufferSize, Func<T, IPublisher<R>> mapper)
        {
            this.actual = actual;
            this.maxConcurrency = maxConcurrency;
            this.delayError = delayError;
            this.bufferSize = bufferSize;

            int lim;
            if (maxConcurrency == int.MaxValue)
            {
                lim = int.MaxValue;
            }
            else
            {
                lim = Math.Max(maxConcurrency >> 1, 1);
            }

            this.limit = lim;
            this.mapper = mapper;
        }

        bool Add(PublisherMergeInner inner)
        {
            return ProcessorHelper.Add(ref subscribers, inner, Terminated);
        }

        void Remove(PublisherMergeInner inner)
        {
            ProcessorHelper.Remove(ref subscribers, inner, Terminated, Empty);
        }

        public void Cancel()
        {
            bp.Cancel();

            SubscriptionHelper.Terminate(ref this.s);

            PublisherMergeInner[] a = ProcessorHelper.Terminate(ref subscribers, Terminated);
            if (a != Terminated)
            {
                foreach (PublisherMergeInner inner in a)
                {
                    inner.Cancel();
                }
            }
        }

        public void OnComplete()
        {
            Volatile.Write(ref done, true);

            Drain();
        }

        void AddError(Exception e)
        {
            if (!ExceptionHelper.Add(ref error, e))
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnError(Exception e)
        {
            AddError(e);

            Volatile.Write(ref done, true);

            Drain();
        }

        public void OnNext(T t)
        {
            IPublisher<R> p;

            p = mapper(t);

            if (p != null)
            {
                if (p is ScalarSource<R>)
                {
                    ScalarSource<R> scalar = (ScalarSource<R>)p;

                    TryScalar(scalar.Get());
                }
                else
                {
                    PublisherMergeInner inner = new PublisherMergeInner(uniqueId++, this, bufferSize);

                    if (Add(inner))
                    {
                        p.Subscribe(inner);
                    } 
                }
            }
        }

        void TryScalar(R value)
        {
            if (bp.TryEnter())
            {
                long r = bp.Requested();

                if (r != 0L) // && (q.producerArray == null || q.IsEmpty()) 
                {
                    actual.OnNext(value);

                    if (r != long.MaxValue)
                    {
                        bp.Produced(1);
                    }

                    int p = produced + 1;
                    if (p == limit)
                    {
                        produced = 0;
                        s.Request(p);
                    }
                    else
                    {
                        produced = p;
                    }

                } else
                {
                    q.InitOnce(bufferSize);

                    q.Offer(value);
                }

                if (bp.Leave())
                {
                    return;
                }
            } else
            {
                q.InitOnce(bufferSize);

                q.Offer(value);

                if (!bp.Enter())
                {
                    return;
                }
            }

            DrainLoop();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                if (maxConcurrency == int.MaxValue)
                {
                    s.Request(long.MaxValue);
                }
                else
                {
                    s.Request(maxConcurrency);
                }
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

        void OnNext(PublisherMergeInner inner, R value)
        {
            if (bp.TryEnter())
            {
                long r = bp.Requested();

                if (r != 0L)
                {
                    actual.OnNext(value);

                    if (r != long.MaxValue)
                    {
                        bp.Produced(1);
                    }

                    inner.Request(1);
                }
                else
                {
                    inner.Enqueue(value);
                }

                if (bp.Leave())
                {
                    return;
                }
            } else
            {
                inner.Enqueue(value);

                if (!bp.Enter())
                {
                    return;
                }
            }

            DrainLoop();
        }

        void OnError(PublisherMergeInner inner, Exception e)
        {
            AddError(e);

            inner.Done();

            Drain();
        }

        void OnComplete(PublisherMergeInner inner)
        {
            inner.Done();

            Drain();
        }

        void Drain()
        {
            if (bp.Enter())
            {
                DrainLoop();
            }
        }

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        void DrainLoop()
        {
            ISubscriber<R> a = actual;

            int missed = 1;
            for (;;)
            {

                bool d = IsDone();
                PublisherMergeInner[] inners = Volatile.Read(ref subscribers);
                int n = inners.Length;

                bool empty = n == 0 && q.IsEmpty();

                if (CheckTerminated(d, empty, a))
                {
                    return;
                }

                long r = bp.Requested();
                long e = 0L;

                if (r != 0L)
                {
                    if (q.IsConsumable())
                    {
                        while (e != r)
                        {
                            d = IsDone();

                            R t;

                            empty = !q.Poll(out t);

                            if (CheckTerminated(d, empty, a))
                            {
                                return;
                            }

                            if (empty)
                            {
                                break;
                            }

                            a.OnNext(t);

                            e++;
                        }

                        if (e != 0L)
                        {
                            r = bp.Produced(e);

                            s.Request(e);

                            e = 0L;
                        }
                    }

                    d = IsDone();
                    inners = Volatile.Read(ref subscribers);
                    n = inners.Length;

                    empty = n == 0 && q.IsEmpty();

                    if (CheckTerminated(d, empty, a))
                    {
                        return;
                    }
                }

                int idx = lastIndex;
                if (idx >= n)
                {
                    idx = 0;
                }

                for (int i = 0; i < n; i++)
                {
                    PublisherMergeInner inner = inners[idx];

                    d = inner.IsDone();
                    empty = inner.IsEmpty();

                    if (d && empty)
                    {
                        Remove(inner);

                        s.Request(1);

                        inners = Volatile.Read(ref subscribers);
                        n = inners.Length;
                        idx--;
                    }
                    else
                    if (r != e && !empty)
                    {
                        while (e != r)
                        {
                            d = inner.IsDone();

                            R t;

                            empty = !inner.Poll(out t);

                            if (d && empty)
                            {
                                Remove(inner);

                                s.Request(1);

                                inners = Volatile.Read(ref subscribers);
                                n = inners.Length;
                                idx--;

                                break;
                            } else
                            if (empty)
                            {
                                break;
                            }

                            a.OnNext(t);

                            e++;
                        }

                        d = inner.IsDone();
                        empty = inner.IsEmpty();

                        if (e == r && d && empty)
                        {
                            Remove(inner);

                            s.Request(1);

                            inners = Volatile.Read(ref subscribers);
                            n = inners.Length;
                            idx--;
                        }

                        if (e != 0L)
                        {
                            r = bp.Produced(e);

                            if (!d)
                            {
                                inner.Request((int)e);
                            }

                            e = 0L;
                        }
                    }

                    idx++;
                    if (idx >= n)
                    {
                        idx = 0;
                    }
                }

                d = IsDone();
                inners = Volatile.Read(ref subscribers);
                n = inners.Length;

                empty = n == 0 && q.IsEmpty();

                if (CheckTerminated(d, empty, a))
                {
                    return;
                }

                lastIndex = idx;

                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        bool CheckTerminated(bool d, bool empty, ISubscriber<R> a)
        {
            if (bp.IsCancelled())
            {               
                return true;
            }

            if (d)
            {
                if (delayError)
                {
                    if (empty)
                    {
                        if (Volatile.Read(ref error) != null)
                        {
                            EmitError(a);
                        } else
                        {
                            a.OnComplete();
                        }
                        return true;
                    }
                }
                else
                {
                    if (Volatile.Read(ref error) != null)
                    {
                        EmitError(a);
                        return true;
                    }
                    else
                    if (empty)
                    {
                        a.OnComplete();
                        return true;
                    }
                }
            }
            return false;
        }

        void EmitError(ISubscriber<R> a)
        {
            Exception e;
            if (ExceptionHelper.Terminate(ref error, out e))
            {
                a.OnError(e);
            }
        }

        sealed class PublisherMergeInner : ISubscriber<R>
        {
            readonly long id;

            readonly PublisherFlatMap<T, R> parent;

            readonly int limit;

            ISubscription s;

            int produced;

            bool done;

            SpscArrayQueueStruct<R> q;

            public PublisherMergeInner(long id, PublisherFlatMap<T, R> parent, int bufferSize)
            {
                this.id = id;
                this.parent = parent;
                this.limit = bufferSize - (bufferSize >> 2);
            }

            public void OnComplete()
            {
                parent.OnComplete(this);
            }

            public void OnError(Exception e)
            {
                parent.OnError(this, e);
            }

            public void OnNext(R t)
            {
                parent.OnNext(this, t);
            }

            internal void Request(int n)
            {
                int r = produced + n;
                if (r >= limit)
                {
                    produced = 0;
                    s.Request(r);
                } else
                {
                    produced = r;
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(parent.bufferSize);
                }
            }

            internal void Cancel()
            {
                SubscriptionHelper.Terminate(ref s);
            }

            internal void Enqueue(R value)
            {
                if (q.array == null)
                {
                    q.InitVolatile(parent.bufferSize);
                }

                if (!q.Offer(value))
                {
                    OnError(BackpressureHelper.MissingBackpressureException());
                }
            }

            internal void Done()
            {
                Volatile.Write(ref done, true);
            }

            internal bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal bool IsEmpty()
            {
                return q.IsEmpty();
            }

            internal bool IsConsumable()
            {
                return q.IsConsumable();
            }

            internal bool Poll(out R value)
            {
                return q.Poll(out value);
            }
        }
    }
}
