using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSwitchMap<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly Func<T, IPublisher<R>> mapper;

        ISubscription s;

        BasicBackpressureStruct bp;

        bool done;

        long index;

        Exception error;

        SpscLinkedArrayQueueStruct<PublisherSwitchMapStruct> q;

        PublisherSwitchMapSubscriber inner;

        public PublisherSwitchMap(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int bufferSize)
        {
            this.actual = actual;
            this.mapper = mapper;
            this.q.Init(bufferSize);
        }

        public void Cancel()
        {
            s.Cancel();
            bp.Cancel();

            PublisherSwitchMapSubscriber i = Volatile.Read(ref inner);
            if (i != Cancelled)
            {
                i = Interlocked.Exchange(ref inner, Cancelled);
                if (i != Cancelled)
                {
                    i?.Cancel();
                }
            }
        }

        public void OnComplete()
        {
            Volatile.Write(ref done, true);
            Drain();
        }

        public void OnError(Exception e)
        {
            error = e;
            Volatile.Write(ref done, true);
            Drain();
        }

        public void OnNext(T t)
        {
            long idx = Interlocked.Increment(ref index);

            PublisherSwitchMapSubscriber curr = inner;

            if (curr == Cancelled)
            {
                return;
            }

            inner?.Cancel();

            IPublisher<R> p;

            try
            {
                p = mapper(t);
            }
            catch (Exception ex)
            {
                OnError(ex);
                return;
            }
            if (p == null)
            {
                OnError(new NullReferenceException("The mapper returned a null IPublisher"));
                return;
            }

            PublisherSwitchMapSubscriber i = new PublisherSwitchMapSubscriber(idx, this, bp.Requested());

            if (Interlocked.CompareExchange(ref inner, i, curr) == curr)
            {
                p.Subscribe(i);
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
                bp.Request(n);
                Drain();
            }
        }

        void Drain()
        {
            if (!bp.Enter())
            {
                return;
            }

            ISubscriber<R> a = actual;
            int missed = 1;

            for (;;)
            {

                long r = bp.Requested();
                long e = 0L;
                
                for (;;)
                {
                    PublisherSwitchMapStruct v;

                    bool empty = q.Peek(out v);

                    if (CheckTerminated(Volatile.Read(ref done), empty, a))
                    {
                        return;
                    }

                    if (empty)
                    {
                        if (e != 0L)
                        {
                            bp.Produced(e);
                        }
                        break;
                    }

                    PublisherSwitchMapSubscriber sender = v.sender;

                    if (Volatile.Read(ref index) == sender.Index())
                    {
                        if (v.done)
                        {
                            Exception ex = sender.Error();
                            if (ex != null)
                            {
                                Cancel();

                                q.Clear();

                                a.OnError(ex);
                                return;
                            }

                            q.Drop();
                        }
                        else
                        if (r != e)
                        {
                            a.OnNext(v.value);

                            q.Drop();
                            e++;
                        }
                        else
                        {
                            if (e != 0L)
                            {
                                bp.Produced(e);

                                sender.Request(e);
                            }
                            break;
                        }
                    }
                    else
                    {
                        q.Drop();
                    }
                }

                if (CheckTerminated(Volatile.Read(ref done), q.IsEmpty(), a))
                {
                    return;
                }

                if (e != 0L)
                {
                    bp.Produced(e);
                }

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
                q.Clear();
                return true;
            }

            if (d)
            {
                Exception e = error;
                if (e != null)
                {
                    q.Clear();

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

        void Signal(PublisherSwitchMapSubscriber sender, R value, bool done)
        {

            if (Volatile.Read(ref index) != sender.Index())
            {
                if (done)
                {
                    Exception e = sender.Error();

                    if (e != null)
                    {
                        RxAdvancedFlowPlugins.OnError(e);
                    }
                }
                return;
            }

            PublisherSwitchMapStruct s = new PublisherSwitchMapStruct();
            s.sender = sender;
            s.value = value;
            s.done = done;

            lock (this)
            {
                q.OfferRef(ref s);
            }

            if (done)
            {
                sender.SetDone();
            }

            Drain();
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        struct PublisherSwitchMapStruct
        {
            internal PublisherSwitchMapSubscriber sender;
            internal R value;
            internal bool done;
        }

        static readonly PublisherSwitchMapSubscriber Cancelled = new PublisherSwitchMapSubscriber(long.MaxValue, null, 0L);

        sealed class PublisherSwitchMapSubscriber : ISubscriber<R>
        {
            readonly long index;

            readonly PublisherSwitchMap<T, R> parent;

            bool done;

            Exception error;

            SingleArbiterStruct arbiter;

            public PublisherSwitchMapSubscriber(long index, PublisherSwitchMap<T, R> parent, long initialRequest)
            {
                this.index = index;
                this.parent = parent;
                this.arbiter.InitRequest(initialRequest);
            }

            public void OnComplete()
            {
                parent.Signal(this, default(R), true);
            }

            public void OnError(Exception e)
            {
                error = e;
                parent.Signal(this, default(R), true);
            }

            public void OnNext(R t)
            {
                parent.Signal(this, t, false);
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            internal long Index()
            {
                return index;
            }

            internal bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal void SetDone()
            {
                Volatile.Write(ref done, true);
            }

            internal void Cancel()
            {
                arbiter.Cancel();
            }

            internal Exception Error()
            {
                return error;
            }

            internal void Request(long n)
            {
                arbiter.Request(n);
            }

            public void OnNext(object element)
            {
                throw new NotImplementedException();
            }
        }
    }
}
