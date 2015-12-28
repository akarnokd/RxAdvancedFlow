using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscriptions
{
    static class SubscriptionHelper
    {
        public static readonly ISubscription Cancelled = new CancelledSubscription();

        public static bool Terminate(ref ISubscription field)
        {
            ISubscription s = Volatile.Read(ref field);
            if (s != Cancelled)
            {
                s = Interlocked.Exchange(ref field, Cancelled);
                if (s != Cancelled)
                {
                    s?.Cancel();
                    return true;
                }
            }

            return false;
        }

        public static bool Set(ref ISubscription field, ISubscription next)
        {
            for (;;)
            {
                ISubscription a = Volatile.Read(ref field);
                if (a == Cancelled)
                {
                    next?.Cancel();
                    return false;
                }
                if (Interlocked.CompareExchange(ref field, next, a) == a)
                {
                    a?.Cancel();

                    return true;
                }
            }
        }

        public static bool Replace(ref ISubscription field, ISubscription next)
        {
            for (;;)
            {
                ISubscription a = Volatile.Read(ref field);
                if (a == Cancelled)
                {
                    next?.Cancel();
                    return false;
                }
                if (Interlocked.CompareExchange(ref field, next, a) == a)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically tries to set the first value on the target field and returns
        /// false if its not null and not the cancelled instance.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="first"></param>
        /// <returns></returns>
        public static bool SetOnce(ref ISubscription field, ISubscription first)
        {
            for (;;)
            {
                ISubscription a = Volatile.Read(ref field);
                if (a == Cancelled)
                {
                    first?.Cancel();
                    return true;
                }

                if (a != null)
                {
                    first?.Cancel();

                    OnSubscribeHelper.ReportSubscriptionSet();
                    return false;
                }

                if (Interlocked.CompareExchange(ref field, first, null) == null)
                {
                    return true;
                }
            }
        }

        public static bool IsTerminated(ref ISubscription field)
        {
            return Volatile.Read(ref field) == Cancelled;
        }

    }

    sealed class CancelledSubscription : ISubscription
    {
        public void Cancel()
        {
            // no op            
        }

        public void Request(long n)
        {
            // no op
        }
    }
}
