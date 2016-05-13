using Reactive.Streams;
using RxAdvancedFlow.subscriptions;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherCollect<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly Action<R, T> collector;

        ISubscription s;

        bool done;

        ScalarDelayedSubscriptionStruct<R> sds;

        public PublisherCollect(ISubscriber<R> actual, R container, Action<R, T> collector)
        {
            this.actual = actual;
            this.collector = collector;
            this.sds.SetValue(container);
        }

        public void Cancel()
        {
            sds.Cancel();
            s.Cancel();
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            sds.Complete(sds.Value(), actual);
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
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

            R c = sds.Value();

            try
            {
                collector(c, t);
            }
            catch (Exception e)
            {
                done = true;
                Cancel();

                actual.OnError(e);
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void Request(long n)
        {
            sds.Request(n, actual);
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
