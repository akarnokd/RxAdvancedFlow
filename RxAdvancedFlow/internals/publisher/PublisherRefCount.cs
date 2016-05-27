using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherRefCount<T> : IPublisher<T>
    {
        readonly IConnectablePublisher<T> source;

        PublisherRefCountConnection connection;

        public PublisherRefCount(IConnectablePublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            PublisherRefCountConnection current;

            for (;;)
            {
                current = Volatile.Read(ref connection);
                if (current == null || current.IsDisposed() || current.Wip() == 0)
                {
                    var next = new PublisherRefCountConnection();

                    if (Interlocked.CompareExchange(ref connection, next, current) != current)
                    {
                        continue;
                    }
                }

                int i = current.Increment();

                if (i == 1 || current.IsDisposed())
                {
                    continue;
                }

                var inner = new PublisherRefCountInner(s, current);

                source.Subscribe(inner);

                if (current.TryConnect())
                {
                    source.Connect(d =>
                    {
                        current.Set(d);
                        current.Decrement();
                    });
                }

                break;
            }
        }

        sealed class PublisherRefCountConnection
        {
            IDisposable d;

            int wip;

            int connected;

            public PublisherRefCountConnection()
            {
                this.wip = 1;
            }

            internal void Set(IDisposable d)
            {
                DisposableHelper.Set(ref this.d, d);
            }

            internal bool IsDisposed()
            {
                return DisposableHelper.IsTerminated(ref d);
            }

            internal int Increment()
            {
                return Interlocked.Increment(ref wip);
            }

            internal void Decrement()
            {
                if (Interlocked.Decrement(ref wip) == 0)
                {
                    DisposableHelper.Terminate(ref d);
                }
            }

            internal int Wip()
            {
                return Volatile.Read(ref wip);
            }

            public bool TryConnect()
            {
                return Volatile.Read(ref connected) == 0
                    && Interlocked.CompareExchange(ref connected, 1, 0) == 0;
            }
        }

        sealed class PublisherRefCountInner : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            internal PublisherRefCountConnection connection;

            ISubscription s;

            int once;

            public PublisherRefCountInner(ISubscriber<T> actual, PublisherRefCountConnection connection)
            {
                this.actual = actual;
                this.connection = connection;
            }

            void Done()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    connection.Decrement();
                }
            }

            public void Cancel()
            {
                s.Cancel();
                Done();
            }

            public void Dispose()
            {
                Cancel();
            }

            public void OnComplete()
            {
                Done();

                actual.OnComplete();
            }

            public void OnError(Exception e)
            {
                Done();

                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (OnSubscribeHelper.SetSubscription(ref this.s, s))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Request(long n)
            {
                s.Request(n);
            }
        }
    }
}
