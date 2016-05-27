using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherAmb<T> : ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly InnerSubscription[] subscriptions;

        int winner = int.MinValue;

        bool cancelled;

        public PublisherAmb(ISubscriber<T> actual, int n)
        {
            this.actual = actual;
            InnerSubscription[] a = new InnerSubscription[n];
            for (int i = 0; i < n; i++)
            {
                a[i] = new InnerSubscription(actual, this, i);
            }
            subscriptions = a;
        }

        public void Subscribe(IPublisher<T>[] sources, int n)
        {

            actual.OnSubscribe(this);

            InnerSubscription[] a = subscriptions;
            for (int i = 0; i < n; i++)
            {
                if (Volatile.Read(ref cancelled) || Volatile.Read(ref winner) != int.MinValue)
                {
                    break;
                }
                sources[i].Subscribe(a[i]);
            }
        }

        public void Cancel()
        {
            Volatile.Write(ref cancelled, true);

            int w = Volatile.Read(ref winner);

            if (w != int.MinValue)
            {
                subscriptions[w].Cancel();
            }
            else {
                foreach (InnerSubscription i in subscriptions)
                {
                    i.Cancel();
                }

            }
        }
        public void Request(long n)
        {
            if (!OnSubscribeHelper.ValidateRequest(n))
            {
                return;
            }

            int w = Volatile.Read(ref winner);

            if (w != int.MinValue)
            {
                subscriptions[w].Request(n);
            }
            else {

                foreach (InnerSubscription i in subscriptions)
                {
                    i.Request(n);
                }

            }
        }

        bool tryWin(int index)
        {
            int w = Volatile.Read(ref winner);

            if (w == int.MinValue)
            {
                return Interlocked.CompareExchange(ref winner, index, int.MinValue) == int.MinValue;
            }

            return false;
        }

        sealed class InnerSubscription : ISubscription, ISubscriber<T>
        {
            readonly ISubscriber<T> actual;

            readonly PublisherAmb<T> parent;

            readonly int index;

            ISubscription s;

            long requested;

            bool won;
            
            public InnerSubscription(ISubscriber<T> actual, PublisherAmb<T> parent, int index)
            {
                this.actual = actual;
                this.parent = parent;
                this.index = index;
            }

            public void Cancel()
            {
                SubscriptionHelper.Terminate(ref s);
            }

            public void OnComplete()
            {
                if (won)
                {
                    actual.OnComplete();
                    return;
                }
                if (parent.tryWin(index))
                {
                    won = true;
                    actual.OnComplete();
                }
            }

            public void OnError(Exception e)
            {
                if (won)
                {
                    actual.OnError(e);
                }
                if (parent.tryWin(index))
                {
                    won = true;
                    actual.OnError(e);
                }
            }

            public void OnNext(T t)
            {
                if (won)
                {
                    actual.OnNext(t);
                }
                if (parent.tryWin(index))
                {
                    won = true;
                    actual.OnNext(t);
                }

            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.SingleSetSubscription(ref this.s, ref requested, s);
            }

            public void Request(long n)
            {
                BackpressureHelper.SingleRequest(ref this.s, ref requested, n);
            }
        }
    }
}
