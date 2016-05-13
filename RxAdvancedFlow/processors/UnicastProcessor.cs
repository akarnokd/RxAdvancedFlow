using Reactive.Streams;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.queues;
using System;
using System.Threading;

namespace RxAdvancedFlow.processors
{
    /// <summary>
    /// A processor implementation that allows only a single Subscriber
    /// and buffers values until one arrives.
    /// </summary>
    /// <typeparam name="T">The input and output value type.</typeparam>
    public sealed class UnicastProcessor<T> : IProcessor<T, T>
    {
        Action onDone;

        SpscLinkedArrayQueueStruct<T> q;

        BasicBackpressureStruct bp;

        bool done;
        Exception error;

        ISubscriber<T> actual;

        int once;

        public UnicastProcessor() : this(16, null)
        {

        }

        public UnicastProcessor(int capacityHint) : this(capacityHint, null)
        {

        }

        public UnicastProcessor(int capacityHint, Action onDone)
        {
            this.q.Init(capacityHint);
            this.onDone = onDone;
        }

        public void OnComplete()
        {
            if (done || bp.IsCancelled())
            {
                return;
            }

            SignalDone();

            Volatile.Write(ref done, true);
            Drain();

        }

        public void OnError(Exception e)
        {
            if (done || bp.IsCancelled())
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            SignalDone();

            error = e;
            Volatile.Write(ref done, true);
            Drain();

            SignalDone();
        }

        public void OnNext(T t)
        {
            if (done || bp.IsCancelled())
            {
                return;
            }

            q.Offer(t);
            Drain();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (done || bp.IsCancelled())
            {
                s.Cancel();
            }
            else
            {
                s.Request(long.MaxValue);
            }
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (Volatile.Read(ref once) == 0
                && Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                s.OnSubscribe(new UnicastSubscription(this));
                Volatile.Write(ref actual, s);
                if (bp.IsCancelled())
                {
                    actual = null;
                    return;
                }
                Drain();
            }
            else
            {
                if (Volatile.Read(ref done))
                {
                    Exception e = error;
                    if (e == null)
                    {
                        s.OnComplete();
                    }
                    else
                    {
                        s.OnError(e);
                    }
                }
                else
                {
                    s.OnError(new InvalidOperationException("UnicastSubject allows only a single Subscriber"));
                }
            }
        }

        void Drain()
        {
            if (!bp.Enter())
            {
                return;
            }

            int missed = 1;
            ISubscriber<T> a = Volatile.Read(ref actual);

            for (;;)
            {
                if (a != null)
                {
                    if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                    {
                        return;
                    }

                    long r = bp.Requested();
                    long e = 0L;

                    while (r != e)
                    {
                        bool d = IsDone();

                        T t;
                        bool empty = !q.Poll(out t);

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

                    if (r == e)
                    {
                        if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                        {
                            return;
                        }
                    }

                    if (e != 0)
                    {
                        bp.Produced(e);
                    }
                }

                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }

                if (a == null)
                {
                    a = Volatile.Read(ref actual);
                }
            }
        }

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        bool CheckTerminated(bool d, bool empty, ISubscriber<T> a)
        {
            if (bp.IsCancelled())
            {
                q.Clear();
                actual = null;
                return true;
            }

            if (d && empty)
            {
                Exception e = error;
                if (e != null)
                {
                    a.OnError(e);
                }
                else
                {
                    a.OnComplete();
                }
                return true;
            }
            return false;
        }

        internal void Cancel()
        {
            bp.Cancel();
            SignalDone();
        }

        void SignalDone()
        {
            Action a = Volatile.Read(ref onDone);
            if (a != null && Interlocked.CompareExchange(ref onDone, null, a) == a)
            {
                a();
            }
        }

        void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                bp.Request(n);
                Drain();
            }
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(ISubscriber subscriber)
        {
            throw new NotImplementedException();
        }

        sealed class UnicastSubscription : ISubscription
        {
            readonly UnicastProcessor<T> parent;

            public UnicastSubscription(UnicastProcessor<T> parent)
            {
                this.parent = parent;
            }

            public void Cancel()
            {
                parent.Cancel();
            }

            public void Request(long n)
            {
                parent.Request(n);
            }
        }
    }
}
