using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherTimeout<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IWorker worker;

        readonly TimeSpan time;

        readonly IPublisher<T> other;

        MultiArbiterStruct arbiter;

        IDisposable d;

        long sharedIndex;

        long localIndex;

        public PublisherTimeout(ISubscriber<T> actual, IWorker worker, TimeSpan time, IPublisher<T> other)
        {
            this.actual = actual;
            this.worker = worker;
            this.other = other;
            this.time = time;
        }

        public void Cancel()
        {
            arbiter.Cancel();
            worker.Dispose();
        }

        internal void CancelMain()
        {
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            worker.Dispose();

            long idx = Interlocked.Increment(ref sharedIndex);

            if (idx != localIndex)
            {
                return;
            }

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            worker.Dispose();

            long idx = Interlocked.Increment(ref sharedIndex);

            if (idx != localIndex)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            d?.Dispose();

            long idx = Interlocked.Increment(ref sharedIndex);

            if (idx != localIndex)
            {
                return;
            }

            localIndex = idx + 1;

            actual.OnNext(t);

            arbiter.Produced(1);

            d = worker.Schedule(() => Run(idx), time);
        }

        internal void ScheduleFirst()
        {
            d = worker.Schedule(() => Run(0), time);
        }

        void Run(long idx)
        {
            if (Interlocked.CompareExchange(ref sharedIndex, idx + 1, idx) == idx)
            {
                arbiter.Set(EmptySubscription.Instance);

                worker.Dispose();

                if (other != null)
                {
                    other.Subscribe(new PublisherTimeoutOther(actual, this));
                }
                else
                {
                    actual.OnError(new TimeoutException());                    
                }
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        public void Set(ISubscription s)
        {
            arbiter.Set(s);
        }

        sealed class PublisherTimeoutOther : ISubscriber<T>
        {
            readonly ISubscriber<T> actual;

            readonly PublisherTimeout<T> parent;

            public PublisherTimeoutOther(ISubscriber<T> actual, PublisherTimeout<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.Set(s);
            }
        }
    }

    sealed class PublisherTimeoutSelector<T, U, V> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, IPublisher<V>> itemDelay;

        readonly IPublisher<T> other;

        MultiArbiterStruct arbiter;

        IDisposable d;

        long sharedIndex;

        long localIndex;

        public PublisherTimeoutSelector(ISubscriber<T> actual, Func<T, IPublisher<V>> itemDelay, IPublisher<T> other)
        {
            this.actual = actual;
            this.itemDelay = itemDelay;
            this.other = other;
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void OnNext(T t)
        {
            d?.Dispose();

            long idx = localIndex;
            if (Interlocked.Increment(ref sharedIndex) == idx)
            {
                localIndex = idx + 1;

                actual.OnNext(t);

                arbiter.Produced(1);

                IPublisher<V> p;

                try {
                    p = itemDelay(t);
                }
                catch (Exception e)
                {
                    localIndex = long.MaxValue;
                    arbiter.Cancel();

                    actual.OnError(e);
                    return;
                }

                if (p == null)
                {
                    localIndex = long.MaxValue;
                    arbiter.Cancel();

                    actual.OnError(new NullReferenceException("The itemDelay returned a null value"));
                    return;
                }

                var to = new PublisherTimeoutSelectorTimeout<V>(this, idx);

                d = to;

                p.Subscribe(to);
            }
        }

        internal void SubscribeFirst(IPublisher<U> p)
        {
            var to = new PublisherTimeoutSelectorTimeout<U>(this, 0L);

            d = to;

            p.Subscribe(to);
        }

        public void OnError(Exception e)
        {
            if (IncrementIndex())
            {
                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnComplete()
        {
            if (IncrementIndex())
            {
                actual.OnComplete();
            }
        }

        public void Request(long n)
        {
            arbiter.Cancel();
        }

        void CancelOther()
        {
            DisposableHelper.Terminate(ref this.d);
        }

        public void Cancel()
        {
            CancelOther();
            arbiter.Cancel();
        }

        void Set(ISubscription s)
        {
            arbiter.Set(s);
        }

        bool CasIndex(long index)
        {
            return Interlocked.CompareExchange(ref sharedIndex, index + 1, index) == index;
        }

        bool IncrementIndex()
        {
            return Interlocked.Increment(ref sharedIndex) == localIndex;
        }

        void Timeout(long index)
        {
            if (CasIndex(index))
            {
                arbiter.Set(EmptySubscription.Instance);

                if (other != null)
                {
                    other.Subscribe(new PublisherTimeoutSelectorOther(actual, this));
                }
                else
                {
                    actual.OnError(new TimeoutException());
                }
            }
        }

        void TimeoutError(Exception e, long index)
        {
            if (CasIndex(index))
            {
                arbiter.Cancel();

                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        sealed class PublisherTimeoutSelectorTimeout<W> : ISubscriber<W>, IDisposable
        {
            readonly PublisherTimeoutSelector<T, U, V> parent;

            readonly long index;

            SingleArbiterStruct arbiter;

            bool done;

            public PublisherTimeoutSelectorTimeout(PublisherTimeoutSelector<T, U, V> parent, long index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public void OnNext(W t)
            {
                if (done)
                {
                    return;
                }
                done = true;
                arbiter.Cancel();

                parent.Timeout(index);
            }

            public void OnError(Exception e)
            {
                parent.TimeoutError(e, index);
            }

            public void OnComplete()
            {
                parent.Timeout(index);
            }

            public void Dispose()
            {
                arbiter.Cancel();
            }
        }

        sealed class PublisherTimeoutSelectorOther : ISubscriber<T>
        {
            readonly ISubscriber<T> actual;

            readonly PublisherTimeoutSelector<T, U, V> parent;

            public PublisherTimeoutSelectorOther(ISubscriber<T> actual, PublisherTimeoutSelector<T, U, V> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.Set(s);
            }
        }
    }
}
