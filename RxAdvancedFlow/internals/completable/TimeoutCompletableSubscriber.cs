using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class TimeoutCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly IScheduler scheduler;

        readonly TimeSpan timeout;

        readonly ICompletable other;

        internal IDisposable upstream;

        IDisposable timer;

        int once;

        public TimeoutCompletableSubscriber(ICompletableSubscriber actual,
            IScheduler scheduler, TimeSpan timeout, ICompletable other)
        {
            this.actual = actual;
            this.scheduler = scheduler;
            this.timeout = timeout;
            this.other = other;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref upstream);
            DisposableHelper.Terminate(ref timer);
        }

        public void OnComplete()
        {
            DisposableHelper.Terminate(ref timer);
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                actual.OnComplete();
            }
        }

        public void OnError(Exception e)
        {
            DisposableHelper.Terminate(ref timer);
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                actual.OnComplete();
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            if (OnSubscribeHelper.SetDisposable(ref upstream, d))
            {
                actual.OnSubscribe(this);

                IDisposable t = scheduler.ScheduleDirect(() =>
                {
                    SubscribeOther();
                }, timeout);

                DisposableHelper.Replace(ref timer, t);
            }
        }

        void SubscribeOther()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                upstream.Dispose();

                if (other != null)
                {
                    other.Subscribe(new OtherCompletableSubscriber(this, actual));
                }
                else
                {
                    actual.OnError(new TimeoutException());
                }
            }
        }

        sealed class OtherCompletableSubscriber : ICompletableSubscriber
        {
            readonly TimeoutCompletableSubscriber parent;

            readonly ICompletableSubscriber actual;

            public OtherCompletableSubscriber(TimeoutCompletableSubscriber parent, ICompletableSubscriber actual)
            {
                this.parent = parent;
                this.actual = actual;
            }

            public void OnSubscribe(IDisposable d)
            {
                DisposableHelper.Replace(ref parent.upstream, d);
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }
        }
    }
}
