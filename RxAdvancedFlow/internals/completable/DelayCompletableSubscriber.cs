using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class DelayCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly IScheduler scheduler;

        readonly TimeSpan delay;

        readonly bool delayError;

        IDisposable d;

        IDisposable task;

        public DelayCompletableSubscriber(ICompletableSubscriber actual,
            IScheduler scheduler, TimeSpan delay, bool delayError)
        {
            this.actual = actual;
            this.scheduler = scheduler;
            this.delay = delay;
            this.delayError = delayError;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref task);
            DisposableHelper.Terminate(ref d);
        }

        public void OnComplete()
        {
            IDisposable t = scheduler.ScheduleDirect(() => actual.OnComplete(), delay);
            DisposableHelper.Replace(ref task, t);
        }

        public void OnError(Exception e)
        {
            if (delayError)
            {
                IDisposable t = scheduler.ScheduleDirect(() => actual.OnError(e), delay);
                DisposableHelper.Replace(ref task, t);
            }
            else
            {
                actual.OnError(e);
            }
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
