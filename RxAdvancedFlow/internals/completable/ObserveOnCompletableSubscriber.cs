using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class ObserveOnCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly IScheduler scheduler;

        IDisposable task;

        IDisposable d;

        public ObserveOnCompletableSubscriber(ICompletableSubscriber actual, 
            IScheduler scheduler)
        {
            this.actual = actual;
            this.scheduler = scheduler;
        }

        public void OnSubscribe(IDisposable d)
        {
            if (OnSubscribeHelper.SetDisposable(ref this.d, d))
            {
                actual.OnSubscribe(d);
            }
        }

        public void OnError(Exception e)
        {
            IDisposable t = scheduler.ScheduleDirect(() =>
            {
                actual.OnError(e);
            });
            DisposableHelper.Replace(ref task, t);
        }

        public void OnComplete()
        {
            IDisposable t = scheduler.ScheduleDirect(() =>
            {
                actual.OnComplete();
            });
            DisposableHelper.Replace(ref task, t);
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref task);
            DisposableHelper.Terminate(ref d);
        }
    }
}
