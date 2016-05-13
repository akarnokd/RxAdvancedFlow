using Reactive.Streams;

namespace RxAdvancedFlow.subscriptions
{
    /// <summary>
    /// An ISubscription implementation that honors backpressure and
    /// allows setting a single value later.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ScalarDelayedSubscription<T> : ISubscription
    {
        readonly ISubscriber<T> actual;

        ScalarDelayedSubscriptionStruct<T> sds;

        public ScalarDelayedSubscription(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public T Value()
        {
            return sds.Value();
        }

        public void Cancel()
        {
            sds.Cancel();
        }

        public void Request(long n)
        {
            sds.Request(n, actual);
        }

        public void Set(T t)
        {
            sds.Complete(t, actual);
        }

        public bool IsCancelled()
        {
            return sds.IsCancelled();
        }
    }
}
