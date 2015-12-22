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

        long missedRequests;

        ISubscription actual;

        public void Cancel()
        {
            SubscriptionHelper.Terminate(ref actual);
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.SingleRequest(ref actual, ref missedRequests, n);
            }
        }

        public bool Set(ISubscription s)
        {
            return BackpressureHelper.SingleSetSubscription(ref actual, ref missedRequests, s);
        }
    }
}
