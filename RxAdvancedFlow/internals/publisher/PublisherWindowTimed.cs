using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using RxAdvancedFlow.processors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherWindowTimedExact<T> : ISubscriber<T>, ISubscription
    {
        readonly int capacityHint;

        readonly TimeSpan time;

        readonly IScheduler scheduler;

        HalfSerializedSubscriberStruct<IProcessor<T, T>> actual;

        IDisposable timer;

        IProcessor<T, T> window;

        ISubscription s;

        int wip;

        long requested;

        int once;

        bool cancelled;

        bool stop;

        public PublisherWindowTimedExact(ISubscriber<IProcessor<T, T>> actual, int capacityHint,
            TimeSpan time, IScheduler scheduler)
        {
            this.actual.Init(actual);
            this.capacityHint = capacityHint;
            this.time = time;
            this.scheduler = scheduler;
            this.wip = 1;
            window = new UnicastProcessor<T>(capacityHint, this.InnerDone);
        }

        internal void Set(IDisposable d)
        {
            DisposableHelper.Set(ref timer, d);
        }

        internal bool IsCancelled()
        {
            return Volatile.Read(ref cancelled);
        }

        public void Cancel()
        {
            Volatile.Write(ref cancelled, true);
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                InnerDone();
            }
        }

        public void OnComplete()
        {
            CancelTimer();

            IProcessor<T, T> w;
            
            lock (this)
            {
                stop = true;
                w = window;
                window = null;
            }

            w?.OnComplete();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            CancelTimer();

            IProcessor<T, T> w;

            lock (this)
            {
                stop = true;
                w = window;
                window = null;
            }

            w?.OnError(e);

            actual.OnError(e);
        }

        internal void Run()
        {
            IProcessor<T, T> w;
            IProcessor<T, T> x;

            lock (this)
            {
                if (stop)
                {
                    return;
                }

                Interlocked.Increment(ref wip);
                w = window;
                if (Volatile.Read(ref cancelled))
                {
                    x = new UnicastProcessor<T>(capacityHint, this.InnerDone);
                    window = x;
                }
                else
                {
                    x = null;
                    window = null;
                }
            }

            w?.OnComplete();

            if (x != null)
            {
                Emit(x);
            }
        }

        public void OnNext(T t)
        {
            IProcessor<T, T> w;

            lock (this)
            {
                w = window;
            }

            w.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                Emit(window);

                Set(scheduler.SchedulePeriodicallyDirect(this.Run, time, time));

                s.Request(long.MaxValue);
            }
        }

        void Emit(IProcessor<T, T> w)
        {
            long r = Volatile.Read(ref requested);
            if (r != 0L)
            {
                actual.OnNext(w);
                if (r != long.MaxValue)
                {
                    Interlocked.Decrement(ref requested);
                }
            } else
            {
                Cancel();

                actual.OnError(BackpressureHelper.MissingBackpressureException());
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.Add(ref requested, n);
            }
        }

        void CancelTimer()
        {
            DisposableHelper.Terminate(ref timer);
        }

        void InnerDone()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                CancelTimer();
                SubscriptionHelper.Terminate(ref s);
            }
        }
    }

    sealed class PublisherWindowTimedSkip<T> : ISubscriber<T>, ISubscription
    {
        HalfSerializedSubscriberStruct<IPublisher<T>> actual;

        readonly TimeSpan timespan;

        readonly TimeSpan timeskip;

        readonly IWorker worker;

        readonly int bufferSize;

        ISubscription s;

        IProcessor<T, T> window;

        int wip;

        int once;

        long requested;

        public PublisherWindowTimedSkip(ISubscriber<IPublisher<T>> actual, 
            TimeSpan timespan, TimeSpan timeskip, IWorker worker, int bufferSize)
        {
            this.actual.Init(actual);
            this.timespan = timespan;
            this.timeskip = timeskip;
            this.bufferSize = bufferSize;
            this.worker = worker;
            this.wip = 1;
            this.window = new UnicastProcessor<T>(bufferSize, this.InnerDone);
        }

        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                InnerDone();
            }
        }

        public void OnComplete()
        {
            IProcessor<T, T> w;

            lock (this)
            {
                w = window;
                window = null;
            }

            w?.OnComplete();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            IProcessor<T, T> w;

            lock (this)
            {
                w = window;
                window = null;
            }

            w?.OnError(e);

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            IProcessor<T, T> w;

            lock (this)
            {
                w = window;
            }

            w?.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {

                Emit(window);

                worker.SchedulePeriodically(this.StartWindow, timeskip, timeskip);

                worker.SchedulePeriodically(this.EndWindow, timespan, timeskip);

                s.Request(long.MaxValue);
            }
        }

        void StartWindow()
        {
            Interlocked.Increment(ref wip);

            IProcessor<T, T> w = new UnicastProcessor<T>(bufferSize, this.InnerDone);

            lock (this)
            {
                window = w;
            }

            Emit(w);
        }

        void EndWindow()
        {
            IProcessor<T, T> w;
            lock (this)
            {
                w = window;
                window = null;
            }

            w?.OnComplete();
        }

        void InnerDone()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                worker.Dispose();
                SubscriptionHelper.Terminate(ref s);
            }
        }

        void Emit(IProcessor<T, T> w)
        {
            long r = Volatile.Read(ref requested);
            if (r != 0L)
            {
                actual.OnNext(w);
                if (r != long.MaxValue)
                {
                    Interlocked.Decrement(ref requested);
                }
            }
            else
            {
                Cancel();

                actual.OnError(BackpressureHelper.MissingBackpressureException());
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.Add(ref requested, n);
            }
        }
    }

    sealed class PublisherWindowTimedOverlap<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<IPublisher<T>> actual;

        readonly TimeSpan timespan;

        readonly TimeSpan timeskip;

        readonly IWorker worker;

        readonly int bufferSize;

        readonly ArrayQueue<IProcessor<T, T>> q;

        ISubscription s;

        BasicBackpressureStruct bp;

        int once;

        int wip;

        public PublisherWindowTimedOverlap(ISubscriber<IPublisher<T>> actual, TimeSpan timespan,
            TimeSpan timeskip, IWorker worker, int bufferSize)
        {
            this.actual = actual;
            this.timespan = timespan;
            this.timeskip = timeskip;
            this.worker = worker;
            this.bufferSize = bufferSize;
            this.q = new ArrayQueue<IProcessor<T, T>>();
            this.wip = 1;
        }


        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                InnerDone();
            }
        }

        void InnerDone()
        {
            if (Interlocked.Decrement(ref wip) == 0)
            {
                SubscriptionHelper.Terminate(ref s);
                worker.Dispose();
            }
        }

        public void OnComplete()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception e)
        {
            throw new NotImplementedException();
        }

        public void OnNext(T t)
        {
            throw new NotImplementedException();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {

                IProcessor<T, T> w = new UnicastProcessor<T>(bufferSize, InnerDone);
                q.Offer(w);

                Emit(w);

                worker.SchedulePeriodically(StartWindow, timeskip, timeskip);
                worker.SchedulePeriodically(EndWindow, timespan, timespan);

                s.Request(long.MaxValue);
            }
        }

        void Emit(IProcessor<T, T> w)
        {
            long r = bp.Requested();
            if (r != 0L)
            {
                actual.OnNext(w);
                if (r != long.MaxValue)
                {
                    bp.Produced(1);
                }
            }
            else
            {
                Cancel();

                actual.OnError(BackpressureHelper.MissingBackpressureException());
            }
        }

        void Drain()
        {

        }

        void StartWindow()
        {

        }

        void EndWindow()
        {

        }

        public void Request(long n)
        {
            bp.Request(n);
        }
    }
}
