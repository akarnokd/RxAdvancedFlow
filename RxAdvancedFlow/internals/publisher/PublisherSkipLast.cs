using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSkipLast<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly int n;

        ArrayQueue<T> queue;

        ISubscription s;

        public PublisherSkipLast(ISubscriber<T> actual, int n)
        {
            this.actual = actual;
            this.n = n;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            ArrayQueue<T> q = queue;
            if (q.Size() == n)
            {
                T t1;
                q.Poll(out t1);

                actual.OnNext(t);
            }
            q.Offer(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);

                s.Request(n);
            }
        }

        public void Request(long n)
        {
            s.Request(n);
        }
    }
}
