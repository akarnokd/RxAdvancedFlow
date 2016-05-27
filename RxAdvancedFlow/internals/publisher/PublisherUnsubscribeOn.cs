using Reactive.Streams;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherUnsubscribeOn<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IScheduler scheduler;
        
        ISubscription s;

        int once;

        public PublisherUnsubscribeOn(ISubscriber<T> actual, IScheduler scheduler)
        {
            this.actual = actual;
            this.scheduler = scheduler;
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
            actual.OnNext(t);
        }

        public void OnError(Exception e)
        {
            ScheduleCancel();

            actual.OnError(e);
        }

        public void OnComplete()
        {
            ScheduleCancel();

            actual.OnComplete();
        }

        public void Request(long n)
        {
            s.Request(n);
        }

        public void Cancel()
        {
            ScheduleCancel();
        }

        void ScheduleCancel()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                scheduler.ScheduleDirect(s.Cancel);
            }
        }
    }
}
