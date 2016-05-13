using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherDebounceTimed<T> : ISubscriber<T>, ISubscription
    {
        readonly IWorker worker;

        readonly TimeSpan timespan;

        ISubscription s;

        LockedSerializedSubscriberStruct<T> actual;

        long index;

        BasicBackpressureStruct bp;

        IDisposable timer;

        public PublisherDebounceTimed(ISubscriber<T> actual, IWorker worker, TimeSpan timespan)
        {
            this.actual.Init(actual);
            this.worker = worker;
            this.timespan = timespan;
        }

        public void Request(long n)
        {
            bp.Request(n);
        }

        public void Cancel()
        {
            s.Cancel();
            worker.Dispose();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void OnNext(T t)
        {
            var d = timer;

            d?.Dispose();

            long i = Interlocked.Increment(ref index);

            PublisherDebounceEmitter emitter = new PublisherDebounceEmitter(i, this, t);

            timer = emitter;

            emitter.Set(worker.Schedule(emitter.Run, timespan));
        }

        public void OnError(Exception e)
        {
            worker.Dispose();

            actual.OnError(e);
        }

        public void OnComplete()
        {
            worker.Dispose();

            actual.OnComplete();
        }

        void Emit(T value, long index)
        {
            if (index == Volatile.Read(ref this.index))
            {
                long r = bp.Requested();
                if (r != 0L)
                {
                    actual.OnNext(value);

                    if (r != long.MaxValue)
                    {
                        bp.Produced(1);
                    }
                    return;
                }

                Cancel();

                actual.OnError(BackpressureHelper.MissingBackpressureException());
            }
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        sealed class PublisherDebounceEmitter : IDisposable
        {
            readonly long index;

            readonly PublisherDebounceTimed<T> parent;

            readonly T value;

            IDisposable d;
            
            public PublisherDebounceEmitter(long index, PublisherDebounceTimed<T> parent, T value)
            {
                this.index = index;
                this.parent = parent;
                this.value = value;
            }

            public void Run()
            {
                parent.Emit(value, index);
            }

            public void Dispose()
            {
                DisposableHelper.Terminate(ref d);
            }

            public void Set(IDisposable d)
            {
                this.d = d;
            }
        }
    }

    sealed class PublisherDebounceSelector<T, U> : ISubscriber<T>, ISubscription
    {
        readonly Func<T, IPublisher<U>> selector;

        ISubscription s;

        LockedSerializedSubscriberStruct<T> actual;

        IDisposable other;

        BasicBackpressureStruct bp;

        long index;

        public PublisherDebounceSelector(ISubscriber<T> actual, Func<T, IPublisher<U>> selector)
        {
            this.actual.Init(actual);
            this.selector = selector;
        }

        public void Cancel()
        {
            s.Cancel();

            DisposableHelper.Terminate(ref other);
        }

        public void OnComplete()
        {
            other?.Dispose();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            other?.Dispose();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            Volatile.Read(ref other)?.Dispose();

            long i = Interlocked.Increment(ref index);

            IPublisher<U> p;

            try
            {
                p = selector(t);
            }
            catch (Exception ex)
            {
                Cancel();
                actual.OnError(ex);
                return;
            }

            if (p == null)
            {
                Cancel();
                actual.OnError(new NullReferenceException("The selector returned a null Publisher"));
                return;
            }

            PublisherDebounceInner inner = new PublisherDebounceInner(t, i, this);

            if (DisposableHelper.Replace(ref other, inner))
            {
                p.Subscribe(inner);
            }

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
            bp.Request(n);
        }

        void Emit(T value, long index)
        {
            if (Volatile.Read(ref this.index) == index)
            {
                actual.OnNext(value);
            }
        }

        void Error(Exception e, long index)
        {
            if (Volatile.Read(ref this.index) == index)
            {
                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        sealed class PublisherDebounceInner : ISubscriber<U>, IDisposable
        {
            readonly PublisherDebounceSelector<T, U> parent;

            readonly long index;

            readonly T value;

            SingleArbiterStruct arbiter;

            bool done;

            public PublisherDebounceInner(T value, long index, PublisherDebounceSelector<T, U> parent)
            {
                this.parent = parent;
                this.value = value;
                this.index = index;
                this.arbiter.InitRequest(long.MaxValue);
            }

            public void Dispose()
            {
                arbiter.Cancel();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }

                parent.Emit(value, index);
            }

            public void OnError(Exception e)
            {
                if (done)
                {
                    RxAdvancedFlowPlugins.OnError(e);
                    return;
                }

                parent.Error(e, index);
            }

            public void OnNext(U t)
            {
                if (done)
                {
                    return;
                }
                done = true;

                arbiter.Cancel();

                parent.Emit(value, index);
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public void OnNext(object element)
            {
                throw new NotImplementedException();
            }
        }
    }
}
