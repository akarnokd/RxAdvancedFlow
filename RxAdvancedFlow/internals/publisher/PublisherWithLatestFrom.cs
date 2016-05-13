using Reactive.Streams;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherWithLatestFrom<T, U, R> : ISubscriber<T>, ISubscription
    {
        readonly Func<T, U, R> combiner;

        HalfSerializedSubscriberStruct<R> actual;

        ISubscription s;

        readonly PublisherWithLatestFromOther other;

        bool done;

        RefNode node;

        public PublisherWithLatestFrom(ISubscriber<R> actual, Func<T, U, R> combiner)
        {
            this.actual.Init(actual);
            this.other = new PublisherWithLatestFromOther(this);
            this.combiner = combiner;
        }

        internal void CancelOther()
        {
            other.Dispose();
        }

        internal void CancelMain()
        {
            SubscriptionHelper.Terminate(ref s);
        }

        public void Cancel()
        {
            CancelOther();

            CancelMain();
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }

            CancelOther();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                return;
            }
            CancelOther();

            actual.OnError(e);
        }

        internal void OtherComplete()
        {
            CancelMain();

            actual.OnComplete();
        }

        internal void OtherError(Exception e)
        {
            CancelMain();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            var n = Volatile.Read(ref node);

            if (n != null)
            {
                R r;

                try {
                    r = combiner(t, n.value);
                } catch (Exception e)
                {
                    done = true;
                    s.Cancel();

                    actual.OnError(e);
                    return;
                }
                actual.OnNext(r);
            }
            else
            {
                s.Request(1);
            }

        }

        public void OnSubscribe(ISubscription s)
        {
            SubscriptionHelper.SetOnce(ref this.s, s);
        }

        public void Request(long n)
        {
            s.Request(n);
        }

        sealed class RefNode
        {
            internal readonly U value;

            public RefNode(U value)
            {
                this.value = value;
            }
        }

        internal ISubscriber<U> GetOther()
        {
            return other;
        }

        void SetValue(U u)
        {
            Volatile.Write(ref node, new RefNode(u));
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        sealed class PublisherWithLatestFromOther : ISubscriber<U>
        {
            readonly PublisherWithLatestFrom<T, U, R> parent;

            SingleArbiterStruct arbiter;

            bool hasValue;

            public PublisherWithLatestFromOther(PublisherWithLatestFrom<T, U, R> parent)
            {
                this.parent = parent;
                arbiter.InitRequest(long.MaxValue);
            }

            public void OnComplete()
            {
                if (!hasValue)
                {
                    parent.OtherComplete();
                }
            }

            public void OnError(Exception e)
            {
                parent.OtherError(e);
            }

            public void OnNext(U t)
            {
                if (!hasValue)
                {
                    hasValue = true;
                }

                parent.SetValue(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            internal void Dispose()
            {
                arbiter.Cancel();
            }

            public void OnNext(object element)
            {
                throw new NotImplementedException();
            }
        }
    }
}
