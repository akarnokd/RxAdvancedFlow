using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class SubscriberToCompletableSubscriber<T> : ISubscriber<T>, IDisposable
    {
        readonly ICompletableSubscriber cs;

        ISubscription s;

        public SubscriberToCompletableSubscriber(ICompletableSubscriber cs)
        {
            this.cs = cs;
        }

        public void Dispose()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            cs.OnComplete();
        }

        public void OnError(Exception e)
        {
            cs.OnError(e);
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        public void OnNext(T t)
        {
            // ignoring values
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                cs.OnSubscribe(this);

                s.Request(long.MaxValue);
            }
        }
    }
}
