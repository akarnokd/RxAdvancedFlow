using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSkip<T> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly long n;

        long remaining;

        public PublisherSkip(ISubscriber<T> actual, long n)
        {
            this.actual = actual;
            this.n = n;
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            long r = remaining;

            if (r == 0)
            {
                actual.OnNext(t);
            }
            else
            {
                remaining = r - 1;
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            actual.OnSubscribe(s);

            if (n != 0L)
            {
                s.Request(n);
            }
        }
    }

    sealed class PublisherSkipTimed<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        IDisposable d;

        bool gate;

        public PublisherSkipTimed(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        internal void CancelMain()
        {
            SubscriptionHelper.Terminate(ref s);
        }

        internal void CancelOther()
        {
            DisposableHelper.Terminate(ref d);
        }

        internal void Set(IDisposable d)
        {
            DisposableHelper.Set(ref this.d, d);
        }

        public void Cancel()
        {
            CancelMain();
            CancelOther();
        }

        public void OnComplete()
        {
            CancelOther();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            CancelOther();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (Volatile.Read(ref gate))
            {
                actual.OnNext(t);
            }
            else
            {
                s.Request(1);
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            s.Request(n);
        }

        internal void Run()
        {
            Volatile.Write(ref gate, true);
        }
    }
}
