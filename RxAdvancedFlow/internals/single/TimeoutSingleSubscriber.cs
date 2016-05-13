using RxAdvancedFlow.internals.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.single
{
    sealed class TimeoutSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingleSubscriber<T> actual;

        readonly TimeSpan timeout;

        readonly IScheduler scheduler;

        readonly ISingle<T> other;

        IDisposable d;

        IDisposable t;

        int once;

        public TimeoutSingleSubscriber(ISingleSubscriber<T> actual, TimeSpan timeout,
            IScheduler scheduler, ISingle<T> other)
        {
            this.actual = actual;
            this.timeout = timeout;
            this.scheduler = scheduler;
            this.other = other;
        }

        internal void Set(IDisposable d)
        {
            DisposableHelper.Replace(ref this.d, d);
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
            DisposableHelper.Terminate(ref t);
        }

        public void OnError(Exception e)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) != 0)
            {
                return;
            }

            DisposableHelper.Terminate(ref this.t);

            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (OnSubscribeHelper.SetDisposable(ref this.d, d))
            {
                actual.OnSubscribe(this);

                IDisposable a = scheduler.ScheduleDirect(() => OnTimeout(), timeout);
                DisposableHelper.Replace(ref t, a);
            }
        }

        public void OnSuccess(T t)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) != 0)
            {
                return;
            }

            DisposableHelper.Terminate(ref this.t);

            actual.OnSuccess(t);
        }

        public void OnTimeout()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) != 0)
            {
                return;
            }

            DisposableHelper.Terminate(ref d);

            if (other != null)
            {
                other.Subscribe(new OtherSingleSubscriber(this));
            }
            else
            {
                actual.OnError(new TimeoutException());
            }
        }

        sealed class OtherSingleSubscriber : ISingleSubscriber<T>
        {
            readonly TimeoutSingleSubscriber<T> parent;

            public OtherSingleSubscriber(TimeoutSingleSubscriber<T> parent)
            {
                this.parent = parent;
            }

            public void OnError(Exception e)
            {
                parent.actual.OnError(e);
            }

            public void OnSubscribe(IDisposable d)
            {
                parent.Set(d);
            }

            public void OnSuccess(T t)
            {
                parent.actual.OnSuccess(t);
            }
        }
    }
}
