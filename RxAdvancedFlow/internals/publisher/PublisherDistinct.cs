using Reactive.Streams;
using System;
using System.Collections.Generic;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherDistinct<T, K> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, K> keyExtractor;

        readonly HashSet<K> set;

        bool done;

        ISubscription s;

        public PublisherDistinct(ISubscriber<T> actual, Func<T, K> keyExtractor, IEqualityComparer<K> comparer)
        {
            this.actual = actual;
            this.keyExtractor = keyExtractor;
            this.set = new HashSet<K>(comparer);
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
                RxAdvancedFlowPlugins.OnError(e);
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

            K k;

            try
            {
                k = keyExtractor(t);
            }
            catch (Exception ex)
            {
                done = true;
                s.Cancel();

                actual.OnError(ex);
                return;
            }
            if (set.Add(k))
            {
                actual.OnNext(t);
            }
            else
            {
                s.Request(1);
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(s);
            }
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
