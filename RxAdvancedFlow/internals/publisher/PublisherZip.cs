using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    interface PublisherZipSupport
    {
        void InnerError(Exception e, PublisherZipInnerSupport inner);

        void Drain();
    }

    interface PublisherZipInnerSupport
    {
        void SetDone();
    }

    sealed class PublisherZip<T, R> : ISubscription, PublisherZipSupport
    {
        readonly ISubscriber<R> actual;

        readonly Func<T[], R> zipper;

        readonly int bufferSize;

        readonly PublisherZipInner<T>[] subscribers;

        BasicBackpressureStruct bp;

        Exception error;

        public PublisherZip(ISubscriber<R> actual, int n, Func<T[], R> zipper, int bufferSize)
        {
            this.actual = actual;
            this.zipper = zipper;
            this.bufferSize = bufferSize;
            PublisherZipInner<T>[] a = new PublisherZipInner<T>[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = new PublisherZipInner<T>(this, bufferSize);
            }
            this.subscribers = a;
        }

        public void Subscribe(IPublisher<T>[] sources, int n)
        {
            PublisherZipInner<T>[] a = subscribers;

            for (int i = 0; i < n; i++)
            {
                if (bp.IsCancelled())
                {
                    return;
                }

                sources[i].Subscribe(a[i]);
            }
        }

        public void Cancel()
        {
            if (!bp.IsCancelled())
            {
                bp.Cancel();
                foreach (PublisherZipInner<T> inner in subscribers)
                {
                    inner.Cancel();
                }
            }
        }

        public void Request(long n)
        {
            bp.Request(n);
            Drain();
        }

        public void InnerError(Exception e, PublisherZipInnerSupport inner)
        {
            if (ExceptionHelper.Add(ref error, e))
            {
                inner.SetDone();
                Drain();
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void Drain()
        {
            if (!bp.Enter())
            {
                return;
            }

            ISubscriber<R> a = actual;
            PublisherZipInner<T>[] inners = subscribers;

            int missed = 1;
            for (;;)
            {

                long r = bp.Requested();
                long e = 0L;

                for (;;)
                {
                    if (bp.IsCancelled())
                    {
                        return;
                    }

                    Exception ex = Volatile.Read(ref error);
                    if (ex != null)
                    {
                        Cancel();

                        ExceptionHelper.Terminate(ref error, out ex);

                        a.OnError(ex);

                        return;
                    }

                    int len = inners.Length;

                    T[] values = new T[len];
                    bool full = true;

                    for (int i = 0; i < len; i++)
                    {
                        PublisherZipInner<T> inner = inners[i];
                        bool d1 = inner.IsDone();

                        T t;
                        bool empty1 = !inner.Peek(out t);
                        if (d1 && empty1)
                        {
                            Cancel();

                            a.OnComplete();

                            return;
                        }

                        if (empty1)
                        {
                            full = false;
                            break;
                        }

                        values[i] = t;
                    }

                    if (full)
                    {
                        if (r != e)
                        {
                            R v;
                            try
                            {
                                v = zipper(values);
                            }
                            catch (Exception ex1)
                            {
                                Cancel();

                                ExceptionHelper.Add(ref error, ex1);
                                ExceptionHelper.Terminate(ref error, out ex1);

                                a.OnError(ex1);
                                return;
                            }

                            a.OnNext(v);

                            foreach (PublisherZipInner<T> inner in inners)
                            {
                                inner.Drop();
                            }

                            e++;
                        } else
                        {
                            if (e != 0)
                            {
                                bp.Produced(e);

                                foreach (PublisherZipInner<T> inner in inners)
                                {
                                    inner.Request(e);
                                }
                            }
                            break;
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
    }

    sealed class PublisherZip2<T1, T2, R> : ISubscription, PublisherZipSupport
    {
        readonly ISubscriber<R> actual;

        readonly Func<T1, T2, R> zipper;

        readonly int bufferSize;

        readonly PublisherZipInner<T1> inner1;

        readonly PublisherZipInner<T2> inner2;

        BasicBackpressureStruct bp;

        Exception error;

        public PublisherZip2(ISubscriber<R> actual, Func<T1, T2, R> zipper, int bufferSize)
        {
            this.actual = actual;
            this.zipper = zipper;
            this.bufferSize = bufferSize;
            this.inner1 = new PublisherZipInner<T1>(this, bufferSize);
            this.inner2 = new PublisherZipInner<T2>(this, bufferSize);
        }

        public void Subscribe(IPublisher<T1> s1, IPublisher<T2> s2)
        {
            s1.Subscribe(inner1);
            if (bp.IsCancelled())
            {
                return;
            }
            s2.Subscribe(inner2);
        }

        public void Cancel()
        {
            if (!bp.IsCancelled())
            {
                bp.Cancel();
                inner1.Cancel();
                inner2.Cancel();
            }
        }

        public void Request(long n)
        {
            bp.Request(n);
            Drain();
        }

        public void InnerError(Exception e, PublisherZipInnerSupport inner)
        {
            if (ExceptionHelper.Add(ref error, e))
            {
                inner.SetDone();
                Drain();
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void Drain()
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
                    if (bp.IsCancelled())
                    {
                        return;
                    }

                    Exception ex = Volatile.Read(ref error);
                    if (ex != null)
                    {
                        Cancel();

                        ExceptionHelper.Terminate(ref error, out ex);

                        a.OnError(ex);

                        return;
                    }

                    bool d1 = inner1.IsDone();
                    T1 t1;
                    bool e1 = !inner1.Peek(out t1);

                    if (d1 && e1)
                    {
                        Cancel();

                        a.OnComplete();

                        return;
                    }

                    bool d2 = inner2.IsDone();
                    T2 t2;
                    bool e2 = !inner2.Peek(out t2);

                    if (d2 && e2)
                    {
                        Cancel();

                        a.OnComplete();

                        return;
                    }

                    if (!e1 && !e2)
                    {
                        if (r != e)
                        {
                            R v;
                            try
                            {
                                v = zipper(t1, t2);
                            }
                            catch (Exception ex1)
                            {
                                Cancel();

                                ExceptionHelper.Add(ref error, ex1);
                                ExceptionHelper.Terminate(ref error, out ex1);

                                a.OnError(ex1);
                                return;
                            }

                            a.OnNext(v);

                            inner1.Drop();
                            inner2.Drop();

                            e++;
                        }
                        else
                        {
                            if (e != 0)
                            {
                                bp.Produced(e);

                                inner1.Request(e);
                                inner2.Request(e);
                            }
                            break;
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
    }

    sealed class PublisherZipInner<T> : ISubscriber<T>, PublisherZipInnerSupport
    {
        readonly PublisherZipSupport parent;

        SpscArrayQueueStruct<T> q;

        bool done;

        SingleArbiterStruct arbiter;

        public PublisherZipInner(PublisherZipSupport parent, int bufferSize)
        {
            this.parent = parent;
            q.Init(bufferSize);
            arbiter.InitRequest(bufferSize);
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void OnNext(T t)
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

        public void OnError(Exception e)
        {
            parent.InnerError(e, this);
        }

        public void OnComplete()
        {
            SetDone();
            parent.Drain();
        }

        public void SetDone()
        {
            Volatile.Write(ref done, true);
        }

        internal bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        internal bool Poll(out T t)
        {
            return q.Poll(out t);
        }

        internal bool Peek(out T t)
        {
            return q.Peek(out t);
        }

        internal void Drop()
        {
            q.Drop();
        }

        internal void Request(long n)
        {
            arbiter.Request(n);
        }

        internal void Cancel()
        {
            arbiter.Cancel();
        }

        internal bool IsEmpty()
        {
            return q.IsEmpty();
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }

}
