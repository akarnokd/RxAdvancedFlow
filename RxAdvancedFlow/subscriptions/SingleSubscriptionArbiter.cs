using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;

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
