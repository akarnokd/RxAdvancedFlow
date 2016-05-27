using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherCombineLatest<T, R> : ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly Func<T[], R> combiner;

        readonly int bufferSize;

        InnerSubscriber[] subscribers;

        SpscLinkedArrayQueueStruct<CombineEntry> q;

        BasicBackpressureStruct bp;

        T[] values;

        ulong[] nonempty;

        int activeCount;

        int completeCount;

        Exception error;

        bool done;

        public PublisherCombineLatest(ISubscriber<R> actual, Func<T[], R> combiner, int capacityHint)
        {
            this.actual = actual;
            this.combiner = combiner;
            this.bufferSize = capacityHint;
            q.Init(capacityHint);
        }

        public void Subscribe(IPublisher<T>[] sources, int n)
        {
            values = new T[n];
            nonempty = new ulong[1 + ((n - 1) >> 4)];

            InnerSubscriber[] a = new InnerSubscriber[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = new InnerSubscriber(this, i);
            }

            subscribers = a;
            actual.OnSubscribe(this);

            for (int i = 0; i < n; i++)
            {
                if (bp.IsCancelled() || LvDone())
                {
                    break;
                }
                sources[i].Subscribe(a[i]);
            }
        }

        bool HasValue(ulong[] nonempty, int index)
        {
            int offset = index >> 4;
            int bit = index & 15;
            return 0 != (nonempty[offset] & (1uL << bit));
        }

        void SetHasValue(ulong[] nonempty, int index)
        {
            int offset = index >> 4;
            int bit = index & 15;
            nonempty[offset] |= (1uL << bit);
        }

        public void Cancel()
        {
            if (bp.TryCancel())
            {
                if (bp.Enter())
                {
                    CancelAll();

                    ClearQueue();
                }
            }
        }

        void CancelAll()
        {
            foreach (InnerSubscriber inner in subscribers)
            {
                inner.Cancel();
            }
        }

        void ClearQueue()
        {
            q.Clear();
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                bp.Request(n);
                Drain();
            }
        }

        void Next(T value, int index)
        {
            T[] vs = values;
            int len = vs.Length;
            ulong[] ne = nonempty;

            lock (this)
            {
                int ac = activeCount;
                if (!HasValue(ne, index))
                {
                    activeCount = ++ac;
                    SetHasValue(ne, index);
                }

                vs[index] = value;

                if (ac != len)
                {
                    return;
                }

                CombineEntry ce = new CombineEntry();
                ce.array = new T[len];
                ce.index = index;

                Array.Copy(vs, 0, ce.array, 0, len);

                q.OfferRef(ref ce);
            }

            Drain();
        }

        void Error(Exception e, int index)
        {
            if (ExceptionHelper.Add(ref error, e))
            {
                SvDone();

                Drain();
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }

        }

        void Complete(int index)
        {
            int len = subscribers.Length;
            ulong[] ne = nonempty;

            lock (this)
            {
                if (HasValue(ne, index))
                {
                    int cc = ++completeCount;

                    if (cc != len)
                    {
                        return;
                    }
                }

                SvDone();
            }

            Drain();
        }

        bool LvDone()
        {
            return Volatile.Read(ref done);
        }

        void SvDone()
        {
            Volatile.Write(ref done, true);
        }

        void Drain()
        {
            if (bp.Enter())
            {
                int missed = 1;

                ISubscriber<R> a = actual;
                InnerSubscriber[] subs = subscribers;

                for (;;) { 

                    if (CheckTerminated(LvDone(), q.IsEmpty(), a))
                    {
                        return;
                    }

                    long r = bp.Requested();
                    bool unbounded = r == long.MaxValue;
                    long e = 0;
                    CombineEntry v;

                    while (r != 0)
                    {
                        bool d = LvDone();

                        bool empty = !q.Poll(out v);

                        if (CheckTerminated(d, empty, a))
                        {
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        R result;

                        try
                        {
                            result = combiner(v.array);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.Add(ref error, ex);
                            done = true;
                            continue;
                        }

                        a.OnNext(result);

                        subs[v.index].Request(1);

                        e++;
                        r--;
                    }

                    if (e != 0 && !unbounded)
                    {
                        bp.Produced(e);
                    }


                    if (bp.TryLeave(ref missed))
                    {
                        break;
                    }
                }
            }
        }

        bool CheckTerminated(bool d, bool empty, ISubscriber<R> a)
        {
            if (bp.IsCancelled())
            {
                CancelAll();
                ClearQueue();
                return true;
            }

            if (d)
            {
                Exception e = Volatile.Read(ref error);
                if (e != null)
                {
                    ExceptionHelper.Terminate(ref error, out e);

                    CancelAll();
                    ClearQueue();

                    a.OnError(e);
                    return true;
                }
                else
                if (empty)
                {
                    a.OnComplete();
                    return true;
                }
            }

            return false;
        }

        sealed class InnerSubscriber : ISubscriber<T>, ISubscription
        {
            readonly int index;

            readonly PublisherCombineLatest<T, R> parent;

            SingleArbiterStruct arbiter;

            public InnerSubscriber(PublisherCombineLatest<T, R> parent, int index)
            {
                this.index = index;
                this.parent = parent;
                arbiter.InitRequest(parent.bufferSize);
            }

            public void Request(long n)
            {
                arbiter.Request(n);
            }

            public void Cancel()
            {
                arbiter.Cancel();
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public void OnNext(T t)
            {
                parent.Next(t, index);
            }

            public void OnError(Exception e)
            {
                parent.Error(e, index);
            }

            public void OnComplete()
            {
                parent.Complete(index);
            }
        }

        struct CombineEntry
        {
            internal T[] array;
            internal int index;
        }

    }
}
