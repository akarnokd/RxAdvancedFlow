using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using RxAdvancedFlow.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherTakeUntil<T, U> : ISubscriber<T>, ISubscription
    {
        LockedSerializedSubscriberStruct<T> actual;

        SingleSubscriptionArbiter main;

        ISubscription other;

        public PublisherTakeUntil(ISubscriber<T> actual)
        {
            this.actual.Init(actual);
        }

        public void Cancel()
        {
            CancelMain();
            CancelOther();
        }

        void CancelMain()
        {
            main.Cancel();
        }

        void CancelOther()
        {
            SubscriptionHelper.Terminate(ref other);
        }

        public void OnComplete()
        {
            CancelOther();

            actual.OnComplete();
        }

        internal void OnCompleteOther()
        {
            CancelMain();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            CancelOther();

            actual.OnError(e);
        }

        internal void OnErrorOther(Exception e)
        {
            CancelMain();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            main.Set(s);
        }

        public void Request(long n)
        {
            main.Request(n);
        }

        internal void SetOther(ISubscription s)
        {
            SubscriptionHelper.SetOnce(ref other, s);
        }

        internal ISubscriber<U> CreateOther()
        {
            return new PublisherTakeUntilOther(this); 
        }

        sealed class PublisherTakeUntilOther : ISubscriber<U>
        {
            readonly PublisherTakeUntil<T, U> main;

            public PublisherTakeUntilOther(PublisherTakeUntil<T, U> main)
            {
                this.main = main;
            }

            public void OnComplete()
            {
                main.OnCompleteOther();
            }

            public void OnError(Exception e)
            {
                main.OnErrorOther(e);
            }

            public void OnNext(U t)
            {
                main.CancelOther();

                main.OnCompleteOther();
            }

            public void OnSubscribe(ISubscription s)
            {
                main.SetOther(s);
            }
        }
    }

    sealed class PublisherTakeUntil<T> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, bool> predicate;

        ISubscription s;

        bool done;

        public PublisherTakeUntil(ISubscriber<T> actual, Func<T, bool> predicate)
        {
            this.actual = actual;
            this.predicate = predicate;
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                return;
            }
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            actual.OnNext(t);

            bool b;
            try
            {
                b = predicate(t);
            }
            catch (Exception e)
            {
                done = true;
                s.Cancel();

                actual.OnError(e);
                return;
            }

            if (b)
            {
                done = true;
                s.Cancel();
                actual.OnComplete();
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(s);
            }
        }
    }

}
