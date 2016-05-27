using Reactive.Streams;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherOnBackpressureLatest<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        BasicBackpressureStruct bp;

        Exception error;
        bool done;

        RefItem item;

        public PublisherOnBackpressureLatest(ISubscriber<T> actual)
        {
            this.actual = actual;
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
            RefItem item = new RefItem(t);

            Volatile.Write(ref this.item, item);

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

                for (;;)
                {
                    bool d = IsDone();

                    RefItem item = Volatile.Read(ref this.item);

                    bool empty = item == null;

                    if (CheckTerminated(d, empty, a))
                    {
                        return;
                    }

                    if (empty)
                    {
                        break;
                    }

                    long r = bp.Requested();

                    if (r != 0L)
                    {
                        item = Interlocked.Exchange(ref this.item, null);

                        a.OnNext(item.value);

                        if (r != long.MaxValue)
                        {
                            bp.Produced(1);
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

        bool CheckTerminated(bool d, bool empty, ISubscriber<T> a)
        {
            if (bp.IsCancelled())
            {
                item = null;
                return true;
            }
            if (d)
            {
                Exception e = error;
                if (e != null)
                {
                    item = null;

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

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        sealed class RefItem
        {
            internal readonly T value;
            internal RefItem(T value)
            {
                this.value = value;
            }
        }
    }
}
