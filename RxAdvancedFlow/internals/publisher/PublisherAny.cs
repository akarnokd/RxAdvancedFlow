using ReactiveStreamsCS;
using RxAdvancedFlow.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherAny<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<bool> actual;

        readonly Func<T, bool> predicate;

        ISubscription s;

        bool done;

        ScalarDelayedSubscriptionStruct<bool> sds;

        public PublisherAny(ISubscriber<bool> actual, Func<T, bool> predicate)
        {
            this.actual = actual;
            this.predicate = predicate;
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                sds.Request(n, actual);
            }
        }

        public void Cancel()
        {
            sds.Cancel();
            s.Cancel();
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

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

                sds.Set(true, actual);
            }
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
            else
            {
                actual.OnError(e);
            }
        }

        public void OnComplete()
        {
            if (!done)
            {
                sds.Set(false, actual);
            }
        }
    }
}
