﻿using ReactiveStreamsCS;
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

        public bool Set(ISubscription s)
        {
            return BackpressureHelper.SingleSetSubscription(ref this.s, ref requested, s);
        }

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
    }
}