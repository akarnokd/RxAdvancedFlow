using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherDematerialize<T> : ISubscriber<Signal<T>>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        T value;

        bool done;

        bool hasValue;

        long requested;

        long produced;

        public PublisherDematerialize(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }

            BackpressureHelper.Produced(ref requested, produced);

            if (hasValue)
            {
                BackpressureHelper.ScalarPostComplete(ref requested, value, actual);
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

        public void OnNext(Signal<T> t)
        {
            if (done)
            {
                return;
            }

            if (hasValue)
            {
                produced++;

                T v = value;

                actual.OnNext(v);
            }
            if (t.IsOnNext())
            {
                value = t.Value;
                hasValue = true;
            }
            else
            if (t.IsOnError())
            {
                done = true;
                s.Cancel();

                actual.OnError(t.Error);
            }
            else
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
                actual.OnSubscribe(this);

                s.Request(1);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (BackpressureHelper.ScalarPostCompleteRequest(ref requested, n, ref value, actual))
                {
                    s.Request(n);
                }
            }
        }
    }
}
