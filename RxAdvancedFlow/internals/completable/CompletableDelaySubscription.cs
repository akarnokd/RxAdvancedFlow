using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableDelaySubscriptionViaObservable<T> : ICompletable
    {
        readonly ICompletable source;

        readonly IObservable<T> other;

        internal CompletableDelaySubscriptionViaObservable(ICompletable source, IObservable<T> other)
        {
            this.source = source;
            this.other = other;
        }

        public void Subscribe(ICompletableSubscriber s)
        {
            var oo = new OtherObserver(s, source);

            s.OnSubscribe(oo);

            oo.SetResource(other.Subscribe(oo));
        }

        sealed class OtherObserver : IObserver<T>, ICompletableSubscriber, IDisposable
        {
            readonly ICompletableSubscriber actual;

            readonly ICompletable source;

            IDisposable other;

            IDisposable main;

            int mode;

            internal OtherObserver(ICompletableSubscriber actual, ICompletable source)
            {
                this.actual = actual;
                this.source = source;
            }

            internal void SetResource(IDisposable o)
            {
                DisposableHelper.SetOnce(ref other, o);
            }

            public void OnNext(T value)
            {
                if (mode != 0)
                {
                    return;
                }
                DisposableHelper.Terminate(ref other);
                OnComplete();
            }

            public void OnError(Exception error)
            {
                if (mode == 2)
                {
                    RxAdvancedFlowPlugins.OnError(error);
                    return;
                }
                mode = 2;
                actual.OnError(error);
            }

            public void OnCompleted()
            {
                if (mode == 0)
                {
                    mode = 1;

                    source.Subscribe(this);
                }
            }

            public void OnSubscribe(IDisposable d)
            {
                OnSubscribeHelper.SetDisposable(ref main, d);
            }

            public void OnComplete()
            {
                if (mode == 1)
                {
                    mode = 2;

                    actual.OnComplete();
                }
            }

            public void Dispose()
            {
                DisposableHelper.Terminate(ref other);
                DisposableHelper.Terminate(ref main);
            }
        }
    }

    sealed class CompletableDelaySubscriptionViaPublisher<T> : ICompletable
    {
        readonly ICompletable source;

        readonly IPublisher<T> other;

        internal CompletableDelaySubscriptionViaPublisher(ICompletable source, IPublisher<T> other)
        {
            this.source = source;
            this.other = other;
        }

        public void Subscribe(ICompletableSubscriber s)
        {
            var oo = new OtherSubscriber(s, source);

            s.OnSubscribe(oo);

            other.Subscribe(oo);
        }

        sealed class OtherSubscriber : ISubscriber<T>, ICompletableSubscriber, IDisposable
        {
            readonly ICompletableSubscriber actual;

            readonly ICompletable source;

            ISubscription other;

            IDisposable main;

            int mode;

            internal OtherSubscriber(ICompletableSubscriber actual, ICompletable source)
            {
                this.actual = actual;
                this.source = source;
            }

            public void OnNext(T value)
            {
                if (mode != 0)
                {
                    return;
                }
                SubscriptionHelper.Terminate(ref other);
                OnComplete();
            }

            public void OnError(Exception error)
            {
                if (mode == 2)
                {
                    RxAdvancedFlowPlugins.OnError(error);
                    return;
                }
                mode = 2;
                actual.OnError(error);
            }

            public void OnCompleted()
            {
                if (mode == 0)
                {
                    mode = 1;

                    source.Subscribe(this);
                }
            }

            public void OnSubscribe(IDisposable d)
            {
                OnSubscribeHelper.SetDisposable(ref main, d);
            }

            public void OnComplete()
            {
                if (mode == 1)
                {
                    mode = 2;

                    actual.OnComplete();
                }
            }

            public void Dispose()
            {
                SubscriptionHelper.Terminate(ref other);
                DisposableHelper.Terminate(ref main);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (OnSubscribeHelper.SetSubscription(ref other, s))
                {
                    s.Request(long.MaxValue);
                }
            }
        }
    }
}
