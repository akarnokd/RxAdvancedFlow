using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherOnBackpressureBufferAll<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        SpscLinkedArrayQueueStruct<T> q;

        BasicBackpressureStruct bp;

        bool done;
        Exception error;

        public PublisherOnBackpressureBufferAll(ISubscriber<T> actual, int bufferSize)
        {
            this.actual = actual;
            this.q.Init(bufferSize);
        }

        public void Cancel()
        {
            bp.Cancel();
            s.Cancel();
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
            q.Offer(t);
            Drain();
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

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        void Drain()
        {
            if (!bp.Enter())
            {
                return;
            }

            ISubscriber<T> a = actual;
            int missed = 1;

            for (;;)
            {

                if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                long r = bp.Requested();
                long e = 0L;

                while (e != r)
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

                if (e == r && CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        bool CheckTerminated(bool d, bool empty, ISubscriber<T> a)
        {
            if (bp.IsCancelled())
            {
                q.Clear();
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

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }

    sealed class PublisherOnBackpressureBufferFixed<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Action onOverflow;

        ISubscription s;

        SpscArrayQueueStruct<T> q;

        BasicBackpressureStruct bp;

        bool done;
        Exception error;

        bool doneOverflow;

        public PublisherOnBackpressureBufferFixed(ISubscriber<T> actual, int bufferSize, Action onOverflow)
        {
            this.actual = actual;
            this.onOverflow = onOverflow;
            this.q.Init(bufferSize);
        }

        public void Cancel()
        {
            bp.Cancel();
            s.Cancel();
        }

        public void OnComplete()
        {
            if (doneOverflow)
            {
                return;
            }

            Volatile.Write(ref done, true);
            Drain();
        }

        public void OnError(Exception e)
        {
            if (doneOverflow)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            error = e;
            Volatile.Write(ref done, true);
            Drain();
        }

        public void OnNext(T t)
        {
            if (doneOverflow)
            {
                return;
            }

            if (!q.Offer(t))
            {
                doneOverflow = true;

                s.Cancel();

                Exception e = BackpressureHelper.MissingBackpressureException();

                try
                {
                    onOverflow?.Invoke();
                }
                catch (Exception ex)
                {
                    e = new AggregateException(e, ex);
                }

                error = e;
                Volatile.Write(ref done, true);
            }
            Drain();
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

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        void Drain()
        {
            if (!bp.Enter())
            {
                return;
            }

            ISubscriber<T> a = actual;
            int missed = 1;

            for (;;)
            {

                if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                long r = bp.Requested();
                long e = 0L;

                while (e != r)
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

                if (e == r && CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        bool CheckTerminated(bool d, bool empty, ISubscriber<T> a)
        {
            if (bp.IsCancelled())
            {
                q.Clear();
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

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
