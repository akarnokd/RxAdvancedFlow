using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class ToSingleCompletableSubscriber<T> : ICompletableSubscriber
    {
        readonly ISingleSubscriber<T> actual;

        readonly Func<T> valueSupplier;

        public ToSingleCompletableSubscriber(ISingleSubscriber<T> actual, Func<T> valueSupplier)
        {
            this.actual = actual;
            this.valueSupplier = valueSupplier;
        }

        public void OnComplete()
        {
            T v;

            try
            {
                v = valueSupplier();
            }
            catch (Exception e)
            {
                actual.OnError(e);
                return;
            }
            actual.OnSuccess(v);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            actual.OnSubscribe(d);
        }
    }
}
