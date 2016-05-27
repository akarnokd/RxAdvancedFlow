using Reactive.Streams;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableAndThenPublisher<T> : IPublisher<T>
    {
        readonly ICompletable first;

        readonly IPublisher<T> second;

        public CompletableAndThenPublisher(ICompletable first, IPublisher<T> second)
        {
            this.first = first;
            this.second = second;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            throw new NotImplementedException();
        }

        sealed class FirstSubscriber : ICompletableSubscriber, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly IPublisher<T> second;

            IDisposable firstDisposable;

            ISubscription secondSubscription;

            long requested;

            public FirstSubscriber(ISubscriber<T> actual, IPublisher<T> second)
            {
                this.actual = actual;
                this.second = second;
            }

            public void OnComplete()
            {
                throw new NotImplementedException();
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnSubscribe(IDisposable d)
            {
                if (OnSubscribeHelper.SetDisposable(ref firstDisposable, d))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void Set(ISubscription s)
            {
                ISubscription a = Interlocked.CompareExchange(ref secondSubscription, s, null);
                if (a == null)
                {
                    long r = Interlocked.Exchange(ref requested, 0);
                    if (r != 0)
                    {
                        s.Request(r);
                    }
                }
                else
                {
                    s?.Cancel();

                    if (a != SubscriptionHelper.Cancelled)
                    {
                        OnSubscribeHelper.ReportSubscriptionSet();
                    }

                }
            }

            public void Request(long n)
            {
                if (OnSubscribeHelper.ValidateRequest(n)) {
                    ISubscription a = Volatile.Read(ref secondSubscription);
                    
                    if (a == null)
                    {
                        BackpressureHelper.Add(ref requested, n);

                        a = Volatile.Read(ref secondSubscription);

                        if (a != null)
                        {
                            long r = Interlocked.Exchange(ref requested, 0);

                            if (r != 0)
                            {
                                a.Request(r);
                            }
                        }
                    }
                    else
                    {
                        a.Request(n);
                    }
                }
            }

            public void Cancel()
            {
                DisposableHelper.Terminate(ref firstDisposable);

                SubscriptionHelper.Terminate(ref secondSubscription);
            }

            sealed class SecondSubscriber : ISubscriber<T>
            {
                readonly FirstSubscriber parent;

                readonly ISubscriber<T> actual;

                public SecondSubscriber(FirstSubscriber parent, ISubscriber<T> actual)
                {
                    this.parent = parent;
                    this.actual = actual;
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.Set(s);
                }

                public void OnNext(T t)
                {
                    throw new NotImplementedException();
                }

                public void OnError(Exception e)
                {
                    throw new NotImplementedException();
                }

                public void OnComplete()
                {
                    throw new NotImplementedException();
                }

            }
        }
    }
}
