using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class UnsubscribeOnCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly IScheduler scheduler;

        IDisposable d;

        public UnsubscribeOnCompletableSubscriber(ICompletableSubscriber actual, IScheduler scheduler)
        {
            this.actual = actual;
            this.scheduler = scheduler;
        }

        public void Dispose()
        {
            scheduler.ScheduleDirect(() =>
            {
                d?.Dispose();
            });
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (OnSubscribeHelper.SetDisposable(ref this.d, d))
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
