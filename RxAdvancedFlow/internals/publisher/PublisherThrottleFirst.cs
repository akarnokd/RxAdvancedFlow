using Reactive.Streams;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherThrottleFirst<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IWorker worker;

        readonly TimeSpan time;

        ISubscription s;

        bool gate;

        public PublisherThrottleFirst(ISubscriber<T> actual, IWorker worker, TimeSpan time)
        {
            this.actual = actual;
            this.worker = worker;
            this.time = time;
        }

        public void Cancel()
        {
            worker.Dispose();
            s.Cancel();
        }

        public void OnComplete()
        {
            worker.Dispose();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            worker.Dispose();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (Volatile.Read(ref gate))
            {
                s.Request(1);
            }
            else
            {
                Volatile.Write(ref gate, true);

                actual.OnNext(t);

                worker.Schedule(this.Run, time);
            }
        }

        void Run()
        {
            Volatile.Write(ref gate, false);
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

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
