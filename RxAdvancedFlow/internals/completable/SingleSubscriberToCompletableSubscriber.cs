using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class SingleSubscriberToCompletableSubscriber<T> : ISingleSubscriber<T> 
    {
        readonly ICompletableSubscriber cs;

        public SingleSubscriberToCompletableSubscriber(ICompletableSubscriber cs)
        {
            this.cs = cs;
        }

        public void OnError(Exception e)
        {
            cs.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            cs.OnSubscribe(d);
        }

        public void OnSuccess(T t)
        {
            cs.OnComplete();
        }
    }
}
