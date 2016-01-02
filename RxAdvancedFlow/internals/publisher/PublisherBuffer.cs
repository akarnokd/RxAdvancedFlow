using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherBufferExact<T, C> : ISubscriber<T>, ISubscription where C : ICollection<T>
    {
        readonly ISubscriber<C> actual;

        readonly Func<C> bufferFactory;

        readonly int size;

        ISubscription s;

        C buffer;

        bool done;

        public PublisherBufferExact(ISubscriber<C> actual, Func<C> bufferFactory, int size)
        {
            this.actual = actual;
            this.bufferFactory = bufferFactory;
            this.size = size;
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            C b = buffer;

            if (b == null)
            {
                try
                {
                    b = bufferFactory();
                }
                catch (Exception e)
                {
                    done = true;
                    s.Cancel();

                    actual.OnError(e);
                    return;
                }
                buffer = b;
            }

            b.Add(t);

            if (b.Count == size)
            {
                buffer = default(C);
                actual.OnNext(b);
            }
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }
            actual.OnError(e);
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            actual.OnComplete();
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                long u = BackpressureHelper.MultiplyCap(size, n);

                s.Request(u);
            }
        }

        public void Cancel()
        {
            s.Cancel();
        }
    }

    sealed class PublisherBufferSkip<T, C> : ISubscriber<T>, ISubscription where C : ICollection<T>
    {
        readonly ISubscriber<C> actual;

        readonly Func<C> bufferFactory;

        readonly int size;

        readonly int skip;

        ISubscription s;

        C buffer;

        bool done;

        long index;

        int once;

        public PublisherBufferSkip(ISubscriber<C> actual, Func<C> bufferFactory, int size, int skip)
        {
            this.actual = actual;
            this.bufferFactory = bufferFactory;
            this.size = size;
            this.skip = skip;
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            C b = buffer;

            if ((index++) % skip == 0)
            {
                try
                {
                    b = bufferFactory();
                }
                catch (Exception e)
                {
                    done = true;
                    s.Cancel();

                    actual.OnError(e);
                    return;
                }
                buffer = b;
            }

            if (b != null)
            {
                b.Add(t);

                if (b.Count == size)
                {
                    buffer = default(C);
                    actual.OnNext(b);
                }
            }
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }
            actual.OnError(e);
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            actual.OnComplete();
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    long u = BackpressureHelper.MultiplyCap(size, n);
                    long v = BackpressureHelper.MultiplyCap(skip - size, n - 1);
                    long w = BackpressureHelper.AddCap(u, v);
                    s.Request(w);
                }
                else
                {
                    long u = BackpressureHelper.MultiplyCap(skip, n);
                    s.Request(u);
                }
            }
        }

        public void Cancel()
        {
            s.Cancel();
        }
    }

    sealed class PublisherBufferOverlap<T, C> : ISubscriber<T>, ISubscription where C : ICollection<T>
    {
        readonly ISubscriber<C> actual;

        readonly Func<C> bufferFactory;

        readonly int size;

        readonly int skip;

        ISubscription s;

        ArrayQueue<C> buffers;

        bool done;

        long index;

        int once;

        long requested;

        bool cancelled;

        long produced;

        public PublisherBufferOverlap(ISubscriber<C> actual, Func<C> bufferFactory, int size, int skip)
        {
            this.actual = actual;
            this.bufferFactory = bufferFactory;
            this.size = size;
            this.skip = skip;
            this.buffers = new ArrayQueue<C>();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }


            C b;

            if ((index++) % skip == 0)
            {

                try
                {
                    b = bufferFactory();
                }
                catch (Exception e)
                {
                    done = true;
                    s.Cancel();

                    actual.OnError(e);
                    return;
                }
                buffers.Offer(b);
            }

            if (buffers.Peek(out b))
            {
                if (b.Count + 1 == size)
                {
                    buffers.Drop();

                    b.Add(t);

                    produced++;

                    actual.OnNext(b);
                }
            }

            buffers.ForEach(v => v.Add(t));
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }
            actual.OnError(e);
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }

            long p = produced;
            if (p != 0L && Volatile.Read(ref requested) != long.MaxValue)
            {
                Interlocked.Add(ref requested, -p);
            }

            BackpressureHelper.PostComplete(ref requested, buffers, actual, ref cancelled);
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (BackpressureHelper.PostCompleteRequest(ref requested, n, buffers, actual, ref cancelled))
                {
                    if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        long u = BackpressureHelper.MultiplyCap(skip, n - 1);
                        long v = BackpressureHelper.AddCap(size, u);
                        s.Request(v);
                    }
                    else
                    {
                        long u = BackpressureHelper.MultiplyCap(skip, n);
                        s.Request(u);
                    }
                }
            }
        }

        public void Cancel()
        {
            Volatile.Write(ref cancelled, true);
            s.Cancel();
        }
    }
}
