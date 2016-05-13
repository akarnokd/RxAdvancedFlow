using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.subscriptions
{
    static class EmptySubscription
    {
        public static readonly ISubscription Instance = new EmptySubscriptionImpl();

        sealed class EmptySubscriptionImpl : ISubscription
        {
            public void Cancel()
            {
                
            }

            public void Request(long n)
            {
                
            }
        }

        public static void Error<T>(ISubscriber<T> s, Exception ex)
        {
            s.OnSubscribe(Instance);
            s.OnError(ex);
        }

        public static void Complete<T>(ISubscriber<T> s)
        {
            s.OnSubscribe(Instance);
            s.OnComplete();
        }

        public static bool NullCheck<T, U>(ISubscriber<T> s, U value) where U : class
        {
            if (value == null)
            {
                Error(s, new NullReferenceException("value supplied is null"));
                return false;
            }

            return true;
        }
    }
}
