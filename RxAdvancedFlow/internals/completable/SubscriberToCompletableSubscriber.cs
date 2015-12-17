using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public void OnNext(T t)
        {
            // 
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                cs.OnSubscribe(this);
            }
        }
    }
}
