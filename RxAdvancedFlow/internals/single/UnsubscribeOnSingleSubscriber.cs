using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class UnsubscribeOnSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingleSubscriber<T> actual;

        readonly IScheduler scheduler;

        IDisposable d;

        int once;

        public UnsubscribeOnSingleSubscriber(ISingleSubscriber<T> actual, IScheduler scheduler)
        {
            this.actual = actual;
            this.scheduler = scheduler;
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                scheduler.ScheduleDirect(() => d.Dispose());
            }
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

        public void OnSuccess(T t)
        {
            actual.OnSuccess(t);
        }
    }
}
