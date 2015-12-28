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
        ScalarDelayedSubscriptionStruct<T> sds;

        public ScalarDelayedSubscription(ISubscriber<T> actual)
        {
            sds.InitSubscriber(actual);
        }

        public T Value()
        {
            return sds.Value();
        }

        public void Cancel()
        {
            sds.Cancel();
        }

        public void Request(long n)
        {
            sds.Request(n);
        }

        public void Set(T t)
        {
            sds.Set(t);
        }

        public bool IsCancelled()
        {
            return sds.IsCancelled();
        }
    }
}
