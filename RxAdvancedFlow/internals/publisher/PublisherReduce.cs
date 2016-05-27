using Reactive.Streams;
using RxAdvancedFlow.subscriptions;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherReduce<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly Func<R, T, R> reducer;

        ISubscription s;

        bool done;

        ScalarDelayedSubscriptionStruct<R> sds;

        public PublisherReduce(ISubscriber<R> actual, R initialValue, Func<R, T, R> reducer)
        {
            this.actual = actual;
            this.reducer = reducer;
            this.sds.SetValue(initialValue);
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
                c = reducer(c, t);
            }
            catch (Exception e)
            {
                done = true;
                Cancel();

                actual.OnError(e);
            }

            sds.SetValue(c);
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

    sealed class PublisherReduce<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, T, T> reducer;

        ISubscription s;

        bool done;

        bool hasValue;

        ScalarDelayedSubscriptionStruct<T> sds;

        public PublisherReduce(ISubscriber<T> actual, Func<T, T, T> reducer)
        {
            this.actual = actual;
            this.reducer = reducer;
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
            if (hasValue)
            {
                sds.Complete(sds.Value(), actual);
            }
            else
            {
                actual.OnComplete();
            }
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

            if (!hasValue)
            {
                hasValue = true;
                sds.SetValue(t);
            }
            else
            {
                T c = sds.Value();

                try
                {
                    c = reducer(c, t);
                }
                catch (Exception e)
                {
                    done = true;
                    Cancel();

                    actual.OnError(e);
                }

                sds.SetValue(c);
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
