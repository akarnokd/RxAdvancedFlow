using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using System.Threading;
using RxAdvancedFlow.internals.subscriptions;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherPublish<T> : IConnectablePublisher<T>
    {
        readonly IPublisher<T> source;

        readonly int bufferSize;

        PublisherPublishMain main;

        public PublisherPublish(IPublisher<T> source, int bufferSize)
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
                    var next = new PublisherPublishMain(this, bufferSize);
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

                break;
            }
        }

        public void Subscribe(ISubscriber<T> s)
        {
            PublisherPublishInner inner = new PublisherPublishInner(s);
            s.OnSubscribe(inner);

            for (;;)
            {
                var current = Volatile.Read(ref main);

                if (current == null || current.IsTerminated())
                {
                    var next = new PublisherPublishMain(this, bufferSize);
                    if (Interlocked.CompareExchange(ref main, next, current) != current)
                    {
                        continue;
                    }
                    current = next;
                }

                if (current.Add(inner))
                {
                    inner.SetParent(current);

                    if (inner.IsCancelled())
                    {
                        current.Remove(inner);
                    }
                    current.Drain();
                    break;
                }
            }
        }

        void ClearCurrent(PublisherPublishMain inner)
        {
            Interlocked.CompareExchange(ref main, null, inner);
        }

        sealed class PublisherPublishMain : ISubscriber<T>, IDisposable
        {
            readonly PublisherPublish<T> parent;

            SpscArrayQueueStruct<T> q;

            PublisherPublishInner[] subscribers;

            static readonly PublisherPublishInner[] Empty = new PublisherPublishInner[0];
            static readonly PublisherPublishInner[] Terminated = new PublisherPublishInner[0];

            int connect;

            SingleArbiterStruct arbiter;

            Exception error;
            bool done;

            int wip;

            public PublisherPublishMain(PublisherPublish<T> parent, int bufferSize)
            {
                this.parent = parent;
                this.q.Init(bufferSize);
                this.arbiter.InitRequest(bufferSize);
            }

            internal bool Add(PublisherPublishInner inner)
            {
                return ProcessorHelper.Add(ref subscribers, inner, Terminated);
            }

            internal void Remove(PublisherPublishInner inner)
            {
                ProcessorHelper.Remove(ref subscribers, inner, Terminated, Empty);
            }

            public bool TryConnect()
            {
                return Volatile.Read(ref connect) == 0 && Interlocked.CompareExchange(ref connect, 1, 0) == 0;
            }

            public bool IsTerminated()
            {
                return Volatile.Read(ref subscribers) == Terminated;
            }

            public void Dispose()
            {
                ProcessorHelper.Terminate(ref subscribers, Terminated);

                arbiter.Cancel();
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
                if (!q.Offer(t))
                {
                    arbiter.Cancel();

                    OnError(BackpressureHelper.MissingBackpressureException());
                } else
                {
                    Drain();
                }
            }
            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal void Drain()
            {
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }

                int missed = 1;

                for (;;)
                {
                    var a = Volatile.Read(ref subscribers);

                    if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                    {
                        return;
                    }

                    int n = 0;
                    long r = long.MaxValue;

                    foreach (var inner in a)
                    {
                        r = Math.Min(r, inner.Requested());
                        n++;
                    }

                    if (n == 0)
                    {
                        if (!q.IsEmpty())
                        {
                            q.Drop();
                            arbiter.Request(1);
                        }
                    }
                    else
                    {
                        long e = 0;

                        while (e != r)
                        {
                            bool d = IsDone();

                            T v;
                            bool empty = !q.Poll(out v);

                            if (CheckTerminated(d, empty, a))
                            {
                                return;
                            }

                            if (empty)
                            {
                                break;
                            }

                            foreach (var inner in a)
                            {
                                inner.OnNext(v);
                            }

                            e++;
                        }

                        if (e == r && CheckTerminated(IsDone(), q.IsEmpty(), a))
                        {
                            return;
                        }

                        if (e != 0)
                        {
                            foreach (var inner in a)
                            {
                                inner.Produced(e);
                            }
                            arbiter.Request(e);
                        }
                    }

                    missed = Interlocked.Add(ref wip, -missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            bool CheckTerminated(bool d, bool empty, PublisherPublishInner[] a)
            {
                if (a == Terminated)
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

                        var b = ProcessorHelper.Terminate(ref subscribers, Terminated);
                        foreach (var inner in b)
                        {
                            inner.OnError(e);
                        }

                        return true;
                    }
                    else
                    if (empty)
                    {
                        var b = ProcessorHelper.Terminate(ref subscribers, Terminated);
                        foreach (var inner in b)
                        {
                            inner.OnComplete();
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        sealed class PublisherPublishInner : ISubscription
        {
            readonly ISubscriber<T> actual;

            PublisherPublishMain parent;

            long requested;

            bool cancelled;

            public PublisherPublishInner(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            internal long Requested()
            {
                return Volatile.Read(ref requested);
            }

            internal void SetParent(PublisherPublishMain parent)
            {
                Volatile.Write(ref this.parent, parent);
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref cancelled);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);

                var m = Volatile.Read(ref parent);

                if (m != null)
                {
                    m.Remove(this);
                    m.Drain();
                }
            }

            public void Request(long n)
            {
                if (OnSubscribeHelper.ValidateRequest(n))
                {
                    BackpressureHelper.Add(ref requested, n);

                    var m = Volatile.Read(ref parent);
                    if (m != null)
                    {
                        m.Drain();
                    }
                }
            }

            internal void OnNext(T value)
            {
                actual.OnNext(value);
            }

            internal void Produced(long n)
            {
                if (Volatile.Read(ref requested) != long.MaxValue)
                {
                    Interlocked.Add(ref requested, n);
                }

            }

            internal void OnError(Exception e)
            {
                actual.OnError(e);
            }

            internal void OnComplete()
            {
                actual.OnComplete();
            }
        }
    }
}
