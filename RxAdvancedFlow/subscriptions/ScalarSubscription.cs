using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.subscriptions
{
    /// <summary>
    /// Subscription that emits a single value to the wrapped ISubscriber
    /// on the first positive request.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public sealed class ScalarSubscription<T> : ISubscription
    {
        readonly T value;

        readonly ISubscriber<T> actual;

        int once;

        public ScalarSubscription(T value, ISubscriber<T> actual)
        {
            this.value = value;
            this.actual = actual;
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    actual.OnNext(value);
                    actual.OnComplete();
                }
            }
        }

        public void Cancel()
        {
            Volatile.Write(ref once, 1);
        }
    }
}
