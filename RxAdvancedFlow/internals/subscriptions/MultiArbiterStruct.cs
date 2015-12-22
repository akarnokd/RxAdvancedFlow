﻿using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscriptions
{
    /// <summary>
    /// Data structure with methods to arbitrate 
    /// between subsequent ISubscriptions and manages
    /// request and cancellation arbitration.
    /// </summary>
    internal struct MultiArbiterStruct
    {
        ISubscription actual;

        long requested;

        ISubscription missedSubscription;

        long missedRequested;

        long missedProduced;

        int wip;

        bool cancelled;

        public void Set(ISubscription next)
        {
            if (Volatile.Read(ref cancelled))
            {
                next?.Cancel();
                return;
            }

            if (Volatile.Read(ref wip) == 0)
            {
                int j = Interlocked.CompareExchange(ref wip, 1, 0);

                if (j == 0)
                {
                    actual = next;
                    long r = requested;
                    if (r != 0)
                    {
                        next.Request(r);
                    }

                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        return;
                    }

                    Drain();

                    return;
                }
            }

            ISubscription a = Interlocked.Exchange(ref missedSubscription, next);

            int i = Interlocked.Increment(ref wip);

            a?.Cancel();

            if (i == 1)
            {
                Drain();
            }
        }

        public void Request(long n)
        {
            if (Volatile.Read(ref cancelled))
            {
                return;
            }

            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.Add(ref missedRequested, n);

                if (Interlocked.Increment(ref wip) == 1)
                {
                    Drain();
                }
            }
        }

        public void Produced(long n)
        {
            if (Volatile.Read(ref cancelled))
            {
                return;
            }

            BackpressureHelper.Add(ref missedProduced, n);

            if (Interlocked.Increment(ref wip) == 1)
            {
                Drain();
            }
        }

        public void Cancel()
        {
            if (Volatile.Read(ref cancelled))
            {
                return;
            }

            Volatile.Write(ref cancelled, true);

            if (Interlocked.Increment(ref wip) == 1)
            {
                Drain();
            }
        }

        void Drain()
        {
            for (;;)
            {

                // missed: check for a non-null value and exchange only then

                ISubscription ms = Volatile.Read(ref missedSubscription);
                if (ms != null)
                {
                    ms = Interlocked.Exchange(ref missedSubscription, null);
                }

                long mr = Volatile.Read(ref missedRequested);
                if (mr != 0)
                {
                    mr = Interlocked.Exchange(ref missedRequested, 0);
                }

                long mp = Volatile.Read(ref missedProduced);
                if (mp != 0)
                {
                    Interlocked.Exchange(ref missedProduced, 0);
                }

                long r = requested;

                if (r != long.MaxValue)
                {
                    long u = BackpressureHelper.AddCap(r, mr);

                    if (u != long.MaxValue)
                    {
                        long v = u - mp;
                        if (v < 0)
                        {
                            v = 0;
                            RxAdvancedFlowPlugins.OnError(new InvalidOperationException("More produced than requested"));
                        }
                        u = v;
                    }

                    requested = u;
                    r = u;
                }

                ISubscription c = actual;

                if (Volatile.Read(ref cancelled))
                {
                    c?.Cancel();
                    ms?.Cancel();
                } else
                if (ms != null)
                {
                    actual = ms;
                    if (r != 0)
                    {
                        ms.Request(r);
                    }
                } else
                if (c != null && mr != 0)
                {
                    c.Request(mr);
                }

                if (Interlocked.Decrement(ref wip) == 0)
                {
                    break;
                }
            }
        }
    }
}