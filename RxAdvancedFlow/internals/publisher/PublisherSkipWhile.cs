using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSkipWhile<T> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, bool> predicate;

        ISubscription s;

        bool skipping;

        bool done;

        public PublisherSkipWhile(ISubscriber<T> actual, Func<T, bool> predicate)
        {
            this.actual = actual;
            this.predicate = predicate;
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                return;
            }
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            if (skipping) {

                bool b;
                try
                {
                    b = predicate(t);
                }
                catch (Exception e)
                {
                    done = true;
                    s.Cancel();

                    actual.OnError(e);
                    return;
                }
                if (b)
                {
                    s.Request(1);
                    return;
                }

                skipping = false;
            }

            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(s);
            }
        }
    }
}
