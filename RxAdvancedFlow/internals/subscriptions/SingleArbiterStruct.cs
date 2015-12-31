using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscriptions
{
    internal struct SingleArbiterStruct
    {
        ISubscription s;

        long requested;

        public void InitRequest(long r)
        {
            requested = r;
        }

        public bool Set(ISubscription s)
        {
            return BackpressureHelper.SingleSetSubscription(ref this.s, ref requested, s);
        }

        /// <summary>
        /// Requests the specified amount immediately or once the actual
        /// subscription arrives (validated).
        /// </summary>
        /// <param name="n"></param>
        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.SingleRequest(ref this.s, ref requested, n);
            }
        }

        public void Cancel()
        {
            SubscriptionHelper.Terminate(ref s);
        }

        public bool IsCancelled()
        {
            return SubscriptionHelper.IsTerminated(ref s);
        }
    }
}
