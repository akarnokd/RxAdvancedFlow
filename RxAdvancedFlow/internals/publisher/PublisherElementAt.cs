using Reactive.Streams;
using RxAdvancedFlow.subscriptions;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherElementAt<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        long index;

        ScalarDelayedSubscriptionStruct<T> sds;

        ISubscription s;

        public PublisherElementAt(ISubscriber<T> actual, long index)
        {
            this.actual = actual;
            this.index = index;
        }

        public void Cancel()
        {
            sds.Cancel();
            s.Cancel();
        }

        public void OnComplete()
        {
            if (index >= 0L)
            {
                actual.OnError(new IndexOutOfRangeException("The source signalled fewer elements than expected"));
            }
        }

        public void OnError(Exception e)
        {
            if (index >= 0L)
            {
                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnNext(T t)
        {
            long i = index;
            index = i - 1;
            if (i == 0L)
            {
                s.Cancel();

                sds.Complete(t, actual);
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
    }

    sealed class PublisherElementAtDefault<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        long index;

        ScalarDelayedSubscriptionStruct<T> sds;

        ISubscription s;

        public PublisherElementAtDefault(ISubscriber<T> actual, long index, T def)
        {
            this.actual = actual;
            this.index = index;
            sds.SetValue(def);
        }

        public void Cancel()
        {
            sds.Cancel();
            s.Cancel();
        }

        public void OnComplete()
        {
            if (index >= 0L)
            {
                sds.Complete(sds.Value(), actual);
            }
        }

        public void OnError(Exception e)
        {
            if (index >= 0L)
            {
                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnNext(T t)
        {
            long i = index;
            index = i - 1;
            if (i == 0L)
            {
                s.Cancel();

                sds.Complete(t, actual);
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
    }
}
