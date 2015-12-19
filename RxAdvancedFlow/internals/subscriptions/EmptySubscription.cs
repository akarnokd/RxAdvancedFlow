using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
