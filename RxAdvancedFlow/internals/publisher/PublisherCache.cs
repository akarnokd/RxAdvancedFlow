using Reactive.Streams;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherCache<T> : IPublisher<T>, ISubscriber<T>
    {
        readonly int capacityHint;

        readonly IPublisher<T> source;

        int once;

        PublisherCacheInner[] subscribers = Empty;

        static readonly PublisherCacheInner[] Empty = new PublisherCacheInner[0];
        static readonly PublisherCacheInner[] Terminated = new PublisherCacheInner[0];

        readonly PublisherCacheItem head;

        PublisherCacheItem tail;

        Exception error;
        bool done;

        public PublisherCache(IPublisher<T> source, int capacityHint)
        {
            this.source = source;
            this.capacityHint = capacityHint;

            PublisherCacheItem h = new PublisherCacheItem(capacityHint);
            head = h;
            tail = h;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            PublisherCacheInner inner = new PublisherCacheInner(s, this, capacityHint, head);

            Add(inner);

            if (inner.IsCancelled())
            {
                Remove(inner);
                return;
            }

            inner.Drain();

            if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                source.Subscribe(this);
            }
        }

        bool Add(PublisherCacheInner inner)
        {
            return ProcessorHelper.Add(ref subscribers, inner, Terminated);
        }

        void Remove(PublisherCacheInner inner)
        {
            ProcessorHelper.Remove(ref subscribers, inner, Terminated, Empty);
        }

        public void OnSubscribe(ISubscription s)
        {
            s.Request(long.MaxValue);
        }

        public void OnNext(T t)
        {
            var s = capacityHint;
            var item = tail;

            var i = item.count;
            if (i == s)
            {
                var b = new PublisherCacheItem(s);
                b.array[0] = t;
                b.count = 1;
                item.svNext(b);
                tail = b;
            }
            else
            {
                item.array[i] = t;
                item.svCount(i + 1);
            }

            foreach(var inner in Volatile.Read(ref subscribers))
            {
                inner.Drain();
            }
        }

        public void OnError(Exception e)
        {
            error = e;
            Volatile.Write(ref done, true);

            var a = ProcessorHelper.Terminate(ref subscribers, Terminated);

            foreach (PublisherCacheInner inner in a)
            {
                inner.Drain();
            }
        }

        public void OnComplete()
        {
            Volatile.Write(ref done, true);

            var a = ProcessorHelper.Terminate(ref subscribers, Terminated);

            foreach (PublisherCacheInner inner in a)
            {
                inner.Drain();
            }
        }

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        Exception Error()
        {
            return error;
        }

        public void Subscribe(ISubscriber subscriber)
        {
            throw new NotImplementedException();
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        sealed class PublisherCacheItem
        {
            internal readonly T[] array;

            internal int count;

            PublisherCacheItem next;

            public PublisherCacheItem(int bufferSize)
            {
                this.array = new T[bufferSize];
            }

            internal void svNext(PublisherCacheItem next)
            {
                Volatile.Write(ref this.next, next);
            }

            internal PublisherCacheItem lvNext()
            {
                return Volatile.Read(ref next);
            }

            internal void svCount(int newCount)
            {
                Volatile.Write(ref count, newCount);
            }

            internal int lvCount()
            {
                return Volatile.Read(ref count);
            }
        }

        sealed class PublisherCacheInner : ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly PublisherCache<T> parent;

            readonly int bufferSize;

            BasicBackpressureStruct bp;

            PublisherCacheItem current;

            int offset;

            public PublisherCacheInner(ISubscriber<T> actual, PublisherCache<T> parent, int bufferSize, PublisherCacheItem h)
            {
                this.actual = actual;
                this.parent = parent;
                this.bufferSize = bufferSize;
                this.current = h;
            }

            public void Cancel()
            {
                bp.Cancel();
            }

            internal bool IsCancelled()
            {
                return bp.IsCancelled();
            }

            public void Request(long n)
            {
                bp.Request(n);
                Drain();
            }

            internal void Drain()
            {
                if (!bp.Enter())
                {
                    return;
                }

                PublisherCache<T> p = parent;
                ISubscriber<T> a = actual;
                PublisherCacheItem item = current;
                int c = offset;

                int missed = 1;
                long r = bp.Requested();
                long e = 0L;

                for (;;)
                {
                    if (IsCancelled())
                    {
                        return;
                    }

                    if (CheckTerminated(p.IsDone(), item.lvCount() == c && item.lvNext() == null, a, p))
                    {
                        return;
                    }

                    while (r != e)
                    {
                        if (IsCancelled())
                        {
                            return;
                        }
                        bool d = p.IsDone();
                        var next = item.lvNext();
                        var f = item.lvCount();
                        bool empty = f == c && next == null;

                        if (CheckTerminated(d, empty, a, p))
                        {
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        if (f == c)
                        {
                            item = next;
                            c = 0;
                        }

                        T v = item.array[c];

                        c++;
                    }

                    if (CheckTerminated(p.IsDone(), item.lvCount() == c && item.lvNext() == null, a, p))
                    {
                        return;
                    }

                    r = bp.Requested();

                    if (e == r)
                    {
                        current = item;
                        offset = c;
                        r = bp.Produced(e);
                        if (r != 0L)
                        {
                            e = 0L;
                            continue;
                        }
                    }

                    missed = bp.Leave(missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            bool CheckTerminated(bool d, bool empty, ISubscriber<T> a, PublisherCache<T> p)
            {
                if (d && empty)
                {
                    Exception ex = p.Error();
                    if (ex != null)
                    {
                        a.OnError(ex);
                    }
                    else
                    {
                        a.OnComplete();
                    }

                    return true;
                }
                return false;
            }
        }
    }
}
