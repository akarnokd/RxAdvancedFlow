using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class ObserveOnSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingleSubscriber<T> actual;

        readonly IScheduler scheduler;

        IDisposable d;

        IDisposable t;

        public ObserveOnSingleSubscriber(ISingleSubscriber<T> actual, IScheduler scheduler)
        {
            this.actual = actual;
            this.scheduler = scheduler;
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
            IDisposable a = scheduler.ScheduleDirect(() =>
            {
                actual.OnSuccess(t);
            });

            DisposableHelper.Replace(ref this.t, a);
        }

        public void OnError(Exception e)
        {
            IDisposable a = scheduler.ScheduleDirect(() =>
            {
                actual.OnError(e);
            });

            DisposableHelper.Replace(ref this.t, a);
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
            DisposableHelper.Terminate(ref t);
        }
    }
}
