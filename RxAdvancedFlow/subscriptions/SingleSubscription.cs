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
    public sealed class SingleSubscription : ISubscription
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
                ISubscription a = Volatile.Read(ref actual);

                if (a == null)
                {
                    BackpressureHelper.Add(ref missedRequests, n);

                    a = Volatile.Read(ref actual);

                    if (a != null)
                    {
                        long r = Interlocked.Exchange(ref missedRequests, 0);

                        if (r != 0)
                        {
                            a.Request(r);
                        }
                    }
                }
                else
                {
                    a.Request(n);
                }
            }
        }

        public bool Set(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref actual, s))
            {
                long r = Interlocked.Exchange(ref missedRequests, 0);
                if (r != 0)
                {
                    s.Request(r);
                }

                return true;
            }

            return false;
        }
    }
}
