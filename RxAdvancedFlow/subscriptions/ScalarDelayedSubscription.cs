using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.subscriptions
{
    /// <summary>
    /// An ISubscription implementation that honors backpressure and
    /// allows setting a single value later.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ScalarDelayedSubscription<T> : ISubscription
    {
        readonly ISubscriber<T> actual;

        int state;

        T value;

        public ScalarDelayedSubscription(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            BackpressureHelper.SetTerminated(ref state);
        }

        public void Request(long n)
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

        public void Set(T t)
        {
            if (BackpressureHelper.SetValue(ref state, ref value, t))
            {
                actual.OnNext(t);
                actual.OnComplete();
            }
        }
    }
}
