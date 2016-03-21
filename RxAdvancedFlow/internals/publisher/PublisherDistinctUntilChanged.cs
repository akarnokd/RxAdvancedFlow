using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherDistinctUntilChanged<T, K> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, K> keyExtractor;

        readonly IEqualityComparer<K> comparer;

        ISubscription s;

        bool done;

        K lastKey;

        bool once;

        public PublisherDistinctUntilChanged(ISubscriber<T> actual, Func<T, K> keyExtractor, IEqualityComparer<K> comparer)
        {
            this.actual = actual;
            this.keyExtractor = keyExtractor;
            this.comparer = comparer;
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(s);
            }
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            K k;

            try {
                k = keyExtractor(t);
            } catch (Exception e)
            {
                done = true;
                s.Cancel();

                actual.OnError(e);
                return;
            }

            if (!once)
            {
                once = true;

                lastKey = k;

                actual.OnNext(t);
            } else
            if (comparer.Equals(lastKey, k))
            {
                lastKey = k;

                s.Request(1);
            }
            else
            {
                lastKey = k;

                actual.OnNext(t);
            }
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            actual.OnError(e);
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }

            actual.OnComplete();
        }
    }
}
