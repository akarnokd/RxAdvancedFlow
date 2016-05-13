using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class DelaySingeSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingleSubscriber<T> actual;

        readonly IScheduler scheduler;

        readonly TimeSpan delay;

        readonly bool delayError;

        IDisposable d;

        IDisposable t;

        public DelaySingeSubscriber(ISingleSubscriber<T> actual, TimeSpan delay, IScheduler scheduler, bool delayError)
        {
            this.actual = actual;
            this.delay = delay;
            this.scheduler = scheduler;
            this.delayError = delayError;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
            DisposableHelper.Terminate(ref t);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (DisposableHelper.SetOnce(ref this.d, d))
            {
                actual.OnSubscribe(this);
            }
            else
            {
                d?.Dispose();
                OnSubscribeHelper.ReportDisposableSet();
            }
        }

        public void OnSuccess(T t)
        {
            IDisposable a = scheduler.ScheduleDirect(() => actual.OnSuccess(t), delay);
            DisposableHelper.Replace(ref this.t, a);
        }

        public void OnError(Exception e)
        {
            if (delayError)
            {
                IDisposable a = scheduler.ScheduleDirect(() => actual.OnError(e), delay);
                DisposableHelper.Replace(ref this.t, a);
            }
            else
            {
                actual.OnError(e);
            }
        }
    }
}
