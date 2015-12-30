using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherMaterialize<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<Signal<T>> actual;

        ISubscription s;

        long requested;

        Signal<T> value;

        public PublisherMaterialize(ISubscriber<Signal<T>> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            Signal<T> v = Signal<T>.CreateOnComplete();
            Volatile.Write(ref value, v);
            BackpressureHelper.ScalarPostComplete(ref requested, v, actual);
        }

        public void OnError(Exception e)
        {
            Signal<T> v = Signal<T>.CreateOnError(e);
            Volatile.Write(ref value, v);
            BackpressureHelper.ScalarPostComplete(ref requested, v, actual);
        }

        public void OnNext(T t)
        {
            actual.OnNext(Signal<T>.CreateOnNext(t));
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        Signal<T> Value()
        {
            return Volatile.Read(ref value);
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (BackpressureHelper.ScalarPostCompleteRequest(ref requested, n, Value(), actual))
                {
                    s.Request(n);
                }
            }
        }
    }
}
