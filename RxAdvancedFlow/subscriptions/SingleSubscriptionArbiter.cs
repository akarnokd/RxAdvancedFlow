using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.subscriptions
{
    /// <summary>
    /// An ISubscription that allows setting a single inner ISubscription
    /// later and accumulates requests and cancellation until then.
    /// </summary>
    public sealed class SingleSubscriptionArbiter : ISubscription
    {

        SingleArbiterStruct a;

        public void Cancel()
        {
            a.Cancel();
        }

        public void Request(long n)
        {
            a.Request(n);
        }

        public bool Set(ISubscription s)
        {
            return a.Set(s);
        }
    }
}
