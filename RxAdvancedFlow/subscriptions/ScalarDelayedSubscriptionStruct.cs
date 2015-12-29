using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.subscriptions
{
    public struct ScalarDelayedSubscriptionStruct<T>
    {
        int state;

        T value;

        public void InitValue(T value)
        {
            this.value = value;
        }

        public T Value()
        {
            return value;
        }

        public void Cancel()
        {
            BackpressureHelper.SetTerminated(ref state);
        }

        public void Request(long n, ISubscriber<T> actual)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (BackpressureHelper.SetRequest(ref state, ref value))
                {
                    actual.OnNext(value);
                    actual.OnComplete();
                }
            }
        }

        public void Set(T t, ISubscriber<T> actual)
        {
            if (BackpressureHelper.SetValue(ref state, ref value, t))
            {
                actual.OnNext(t);
                actual.OnComplete();
            }
        }

        public bool IsCancelled()
        {
            return BackpressureHelper.IsTerminated(ref state);
        }
    }
}
