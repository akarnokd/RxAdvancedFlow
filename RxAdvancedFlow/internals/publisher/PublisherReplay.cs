using System;
using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherReplay<T> : IConnectablePublisher<T>
    {
        readonly IPublisher<T> source;

        readonly int bufferSize;

        PublisherReplayMain main;

        public PublisherReplay(IPublisher<T> source, int bufferSize)
        {
            this.source = source;
            this.bufferSize = bufferSize;
        }

        public void Connect(Action<IDisposable> onConnect)
        {

            for (;;)
            {
                var current = Volatile.Read(ref main);

                if (current == null || current.IsTerminated())
                {
                    var next = new PublisherReplayMain(bufferSize);
                    if (Interlocked.CompareExchange(ref main, next, current) != current)
                    {
                        continue;
                    }
                    current = next;
                }

                onConnect(current);

                if (current.TryConnect())
                {
                    source.Subscribe(current);
                }
            }
        }

        public void Subscribe(ISubscriber<T> s)
        {
            var current = Volatile.Read(ref main);

            if (current == null)
            {
                var next = new PublisherReplayMain(bufferSize);

                current = Interlocked.CompareExchange(ref main, next, null);
                if (current == null)
                {
                    current = next;
                }
            }

            current.Subscribe(s);
        }

        sealed class PublisherReplayMain : ISubscriber<T>, IDisposable
        {
            readonly int bufferSize;

            readonly Node head;

            SingleArbiterStruct arbiter;

            Node tail;

            bool done;
            Exception error;

            PublisherReplayInner[] subscribers;

            static readonly PublisherReplayInner[] Empty = new PublisherReplayInner[0];
            static readonly PublisherReplayInner[] Terminated = new PublisherReplayInner[0];

            int connected;

            public PublisherReplayMain(int bufferSize)
            {
                arbiter.InitRequest(long.MaxValue);
                this.bufferSize = bufferSize;
                Node h = new Node(bufferSize);
                head = h;
                tail = h;
            }

            internal bool IsTerminated()
            {
                return Volatile.Read(ref subscribers) == Terminated;
            }

            internal bool TryConnect()
            {
                return Volatile.Read(ref connected) == 0 && Interlocked.CompareExchange(ref connected, 1, 0) == 0;
            }

            public void Dispose()
            {
                arbiter.Cancel();
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                foreach (var inner in ProcessorHelper.Terminate(ref subscribers, Terminated))
                {
                    Drain(inner);
                }
            }

            public void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                foreach (var inner in ProcessorHelper.Terminate(ref subscribers, Terminated))
                {
                    Drain(inner);
                }
            }

            public void OnNext(T t)
            {
                Node n = tail;
                int c = n.LpCount();
                if (c == bufferSize)
                {
                    Node u = new Node(bufferSize);
                    u.array[0] = t;
                    u.SpCount(1);
                    tail = u;
                    n.SvNext(u);
                }
                else
                {
                    n.array[c] = t;
                    n.SvCount(c + 1);
                }

                foreach (var inner in Volatile.Read(ref subscribers))
                {
                    Drain(inner);
                }
            }

            internal void Subscribe(ISubscriber<T> s)
            {
                PublisherReplayInner inner = new PublisherReplayInner(s, this);

                s.OnSubscribe(inner);

                Add(inner);

                if (inner.IsCancelled())
                {
                    Remove(inner);
                }
                else
                {
                    Drain(inner);
                }
            }

            void Drain(PublisherReplayInner inner)
            {
                if (!inner.Enter())
                {
                    return;
                }

                int missed = 1;

                ISubscriber<T> a = inner.actual;

                for (;;)
                {

                    bool d = Volatile.Read(ref done);

                    Node current = inner.current;
                    int offset = inner.offset;
                    T[] array = current.array;

                    bool empty = current.LvNext() == null && current.LvCount() == offset;

                    if (CheckTerminated(d, empty, inner, a))
                    {
                        return;
                    }

                    long r = inner.Requested();
                    long e = 0L;

                    while (e != r)
                    {
                        d = Volatile.Read(ref done);

                        current = inner.current;
                        offset = inner.offset;

                        Node next = current.LvNext();
                        int c = current.LvCount();

                        empty = next == null && c == offset;

                        if (CheckTerminated(d, empty, inner, a))
                        {
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        if (c == offset && next != null)
                        {
                            current = next;
                            array = next.array;
                            offset = 0;
                        }

                        T t = array[offset];

                        a.OnNext(t);

                        offset++;
                        e++;
                    }

                    if (e == r)
                    {
                        d = Volatile.Read(ref done);

                        current = inner.current;
                        offset = inner.offset;

                        empty = current.LvNext() == null && current.LvCount() == offset;

                        if (CheckTerminated(d, empty, inner, a))
                        {
                            return;
                        }
                    }

                    if (e != 0)
                    {
                        inner.Produced(e);
                    }

                    inner.current = current;
                    inner.offset = offset;

                    missed = inner.Leave(missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            bool CheckTerminated(bool d, bool empty, PublisherReplayInner inner, ISubscriber<T> a)
            {
                if (inner.IsCancelled())
                {
                    inner.current = null;
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

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            void Add(PublisherReplayInner inner)
            {
                ProcessorHelper.Add(ref subscribers, inner, Terminated);
            }

            void Remove(PublisherReplayInner inner)
            {
                ProcessorHelper.Remove(ref subscribers, inner, Terminated, Empty);
            }

            sealed class Node
            {
                internal readonly T[] array;

                int count;

                Node next;

                public Node(int capacity)
                {
                    array = new T[capacity];
                }

                internal int LvCount()
                {
                    return Volatile.Read(ref count);
                }

                internal int LpCount()
                {
                    return count;
                }

                internal void SvCount(int newCount)
                {
                    Volatile.Write(ref count, newCount);
                }

                internal void SpCount(int newCount)
                {
                    this.count = newCount;
                }

                internal Node LvNext()
                {
                    return Volatile.Read(ref next);
                }

                internal void SvNext(Node newNext)
                {
                    Volatile.Write(ref next, newNext);
                }
            }

            sealed class PublisherReplayInner : ISubscription
            {
                internal readonly ISubscriber<T> actual;

                readonly PublisherReplayMain parent;

                BasicBackpressureStruct bp;

                internal Node current;

                internal int offset;

                public PublisherReplayInner(ISubscriber<T> actual, PublisherReplayMain parent)
                {
                    this.actual = actual;
                    this.parent = parent;
                }
                
                public void Cancel()
                {
                    if (!bp.IsCancelled())
                    {
                        bp.Cancel();
                        parent.Remove(this);
                    }
                }

                public bool IsCancelled()
                {
                    return bp.IsCancelled();
                }

                public void Request(long n)
                {
                    bp.Request(n);
                    parent.Drain(this);
                }

                public long Requested()
                {
                    return bp.Requested();
                }

                public void Produced(long n)
                {
                    bp.Produced(n);
                }

                public bool Enter()
                {
                    return bp.Enter();
                }

                public int Leave(int actions)
                {
                    return bp.Leave(actions);
                }
            }
        }
    }
}
