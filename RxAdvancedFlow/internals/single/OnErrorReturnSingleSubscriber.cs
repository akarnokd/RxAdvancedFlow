using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class OnErrorReturnSingleSubscriber<T> : ISingleSubscriber<T>
    {
        readonly ISingleSubscriber<T> actual;

        readonly Func<T> valueSupplier;

        public OnErrorReturnSingleSubscriber(ISingleSubscriber<T> actual, Func<T> valueSupplier)
        {
            this.actual = actual;
            this.valueSupplier = valueSupplier;
        }

        public void OnError(Exception e)
        {
            T v;

            try
            {
                v = valueSupplier();
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            actual.OnSuccess(v);
        }

        public void OnSubscribe(IDisposable d)
        {
            actual.OnSubscribe(d);
        }

        public void OnSuccess(T t)
        {
            actual.OnSuccess(t);
        }
    }
}
