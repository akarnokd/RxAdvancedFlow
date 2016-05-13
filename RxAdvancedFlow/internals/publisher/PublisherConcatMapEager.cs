using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherConcatMapEager<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly Func<T, IPublisher<R>> mapper;

        readonly int bufferSize;

        ISubscription s;

        bool done;

        Exception error;

        BasicBackpressureStruct bp;

        SpscLinkedArrayQueueStruct<PublisherConcatMapEagerInner> q;

        public PublisherConcatMapEager(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int bufferSize)
        {
            this.actual = actual;
            this.mapper = mapper;
            this.bufferSize = bufferSize;
            this.q.Init(bufferSize);
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
            IPublisher<R> p;

            try
            {
                p = mapper(t);
            }
            catch (Exception e)
            {
                s.Cancel();

                OnError(e);
                return;
            }

            if (p == null)
            {
                s.Cancel();

                OnError(new NullReferenceException("The mapper returned a null Publisher"));
                return;
            }

            PublisherConcatMapEagerInner inner = new PublisherConcatMapEagerInner(this, bufferSize);

            if (!bp.IsCancelled())
            {
                q.Offer(inner);

                if (bp.IsCancelled())
                {
                    ClearDrain();
                }
                else
                {
                    p.Subscribe(inner);

                    Drain();
                }
            }
        }
        
        public void OnError(Exception e)
        {
            if (ExceptionHelper.Add(ref error, e))
            {
                Volatile.Write(ref done, true);
                Drain();
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        void InnerError(Exception e, PublisherConcatMapEagerInner inner)
        {
            if (ExceptionHelper.Add(ref error, e))
            {
                Volatile.Write(ref done, true);
                inner.SetDone();

                Drain();
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnComplete()
        {
            Volatile.Write(ref done, true);
            Drain();
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                bp.Request(n);
                Drain();
            }
        }

        public void Cancel()
        {
            if (bp.IsCancelled())
            {
                return;
            }

            s.Cancel();
            bp.Cancel();

            ClearDrain();
        }

        void ClearDrain()
        {
            if (bp.Enter())
            {
                int missed = 1;
                for (;;)
                {
                    ClearQueue();

                    missed = bp.Leave(missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

        }

        void ClearQueue()
        {
            PublisherConcatMapEagerInner inner;

            while (q.Poll(out inner))
            {
                inner.Cancel();
            }
        }

        bool CheckError(ISubscriber<R> a)
        {
            Exception ex = Volatile.Read(ref error);
            if (ex != null)
            {
                if (ExceptionHelper.Terminate(ref error, out ex))
                {
                    s.Cancel();
                    bp.Cancel();
                    ClearQueue();

                    a.OnError(ex);
                }

                return true;
            }
            return false;
        }

        bool CheckTerminated(ISubscriber<R> a)
        {
            if (bp.IsCancelled())
            {
                ClearQueue();
                return true;
            }

            if (Volatile.Read(ref done)) {
                if (CheckError(a))
                {
                    return true;
                }
                else
                if (q.IsEmpty())
                {
                    a.OnComplete();
                    return true;
                }

            }
            return false;
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
                if (!CheckTerminated(a))
                { 
                    long r = bp.Requested();
                    long e = 0L;
                    long f = 0L;

                    PublisherConcatMapEagerInner inner;
                    q.Peek(out inner);

                    while (e != r)
                    {
                        if (bp.IsCancelled())
                        {
                            break;
                        }

                        bool md = Volatile.Read(ref done);
                        bool d = inner.IsDone();

                        R v;
                        bool empty = !inner.Poll(out v);

                        if (md || d)
                        {
                            if (Volatile.Read(ref error) != null)
                            {
                                break;
                            }
                        }
                        if (d && empty)
                        {
                            f = 0L;

                            q.Drop();

                            if (!q.Peek(out inner))
                            {
                                break;
                            }
                            continue;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(v);

                        e++;
                        f++;
                    }

                    if (e == r && inner.IsDone() && inner.IsEmpty())
                    {
                        q.Drop();
                    }

                    if (!CheckTerminated(a))
                    {
                        if (e != 0L)
                        {
                            bp.Produced(e);

                            inner.Request(f);
                        }
                    }
                }


                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        sealed class PublisherConcatMapEagerInner : ISubscriber<R>, ISubscription
        {
            readonly PublisherConcatMapEager<T, R> parent;

            readonly int limit;

            SingleArbiterStruct arbiter;

            bool done;

            SpscArrayQueueStruct<R> q;

            long produced;

            public PublisherConcatMapEagerInner(PublisherConcatMapEager<T, R> parent, int bufferSize)
            {
                this.parent = parent;
                this.arbiter.InitRequest(bufferSize);
                q.Init(bufferSize);
                this.limit = bufferSize - (bufferSize >> 2);
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                parent.Drain();
            }

            public void OnError(Exception e)
            {
                parent.InnerError(e, this);
            }

            internal void SetDone()
            {
                Volatile.Write(ref done, true);
            }

            public void OnNext(R t)
            {
                if (!q.Offer(t))
                {
                    arbiter.Cancel();

                    OnError(BackpressureHelper.MissingBackpressureException());
                }
                else
                {
                    parent.Drain();
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public void Request(long n)
            {
                long p = produced + n;

                if (p >= limit)
                {
                    produced = 0;
                    arbiter.Request(p);
                }
                else
                {
                    produced = p;
                }

            }

            public void Cancel()
            {
                arbiter.Cancel();
            }

            internal bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal bool IsEmpty()
            {
                return q.IsEmpty();
            }

            internal bool Poll(out R value)
            {
                return q.Poll(out value);
            }

            public void OnNext(object element)
            {
                throw new NotImplementedException();
            }
        }
    }
}
