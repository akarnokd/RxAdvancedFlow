using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals
{
    /// <summary>
    /// Utility methods to help with the OnSubscribe calls.
    /// </summary>
    static class OnSubscribeHelper
    {
        /// <summary>
        /// Reports an exception that indicates an IDisposable field is already set
        /// to the RxAdvancedFlowPlugins.
        /// </summary>
        public static void ReportDisposableSet()
        {
            RxAdvancedFlowPlugins.OnError(new Exception("IDisposable already set"));
        }

        /// <summary>
        /// Reports an exception that indicates an ISubscription field is already set
        /// to the RxAdvancedFlowPlugins.
        /// </summary>
        public static void ReportSubscriptionSet()
        {
            RxAdvancedFlowPlugins.OnError(new Exception("ISubscription already set"));
        }

        /// <summary>
        /// Reports an exception that indicates the requested amount was non-positive
        /// to the RxAdvancedFlowPlugins.
        /// </summary>
        /// <param name="n">The requested amount.</param>
        public static void ReportBadRequest(long n)
        {
            RxAdvancedFlowPlugins.OnError(new Exception("n > 0 required but it was " + n));
        }

        /// <summary>
        /// Sets a disposable reference field or reports an error if already set.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool SetDisposable(ref IDisposable d, IDisposable v)
        {
            if (v == null)
            {
                RxAdvancedFlowPlugins.OnError(new NullReferenceException("v"));
                return false;
            }
            if (d != null)
            {
                ReportDisposableSet();
                return false;
            }
            d = v;
            return true;
        }

        /// <summary>
        /// Sets a subscription reference field or reports an error if already set.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool SetSubscription(ref ISubscription d, ISubscription v)
        {
            if (v == null)
            {
                RxAdvancedFlowPlugins.OnError(new NullReferenceException("v"));
                return false;
            }
            if (d != null)
            {
                ReportSubscriptionSet();
                return false;
            }
            d = v;
            return true;
        }

        /// <summary>
        /// Validates the request amount which should be a positive value.
        /// </summary>
        /// <param name="n"></param>
        /// <returns>If the validation was successful</returns>
        public static bool ValidateRequest(long n)
        {
            if (n <= 0)
            {
                ReportBadRequest(n);

                return false;
            }
            return true;
        }
    }
}
