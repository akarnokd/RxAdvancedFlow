using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using RxAdvancedFlow.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSkipUntil<T, U> : ISubscriber<T>, ISubscription
    {
        LockedSerializedSubscriberStruct<T> actual;

        SingleSubscriptionArbiter main;

        ISubscription other;

        bool gate;

        public PublisherSkipUntil(ISubscriber<T> actual)
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
            Volatile.Write(ref gate, true);
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
            if (Volatile.Read(ref gate))
            {
                actual.OnNext(t);
            }
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
            return new PublisherSkipUntilOther(this); 
        }

        sealed class PublisherSkipUntilOther : ISubscriber<U>
        {
            readonly PublisherSkipUntil<T, U> main;

            public PublisherSkipUntilOther(PublisherSkipUntil<T, U> main)
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
}
