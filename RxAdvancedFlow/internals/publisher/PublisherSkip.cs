using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSkip<T> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly long n;

        long remaining;

        public PublisherSkip(ISubscriber<T> actual, long n)
        {
            this.actual = actual;
            this.n = n;
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
            long r = remaining;

            if (r == 0)
            {
                actual.OnNext(t);
            }
            else
            {
                remaining = r - 1;
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            actual.OnSubscribe(s);

            if (n != 0L)
            {
                s.Request(n);
            }
        }
    }
}
