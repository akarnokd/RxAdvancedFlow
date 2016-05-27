using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherTakeWhile<T> : ISubscriber<T>
    {
        readonly ISubscriber<T> actual;

        readonly Func<T, bool> predicate;

        ISubscription s;

        bool done;

        public PublisherTakeWhile(ISubscriber<T> actual, Func<T, bool> predicate)
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

            if (!b)
            {
                done = true;
                s.Cancel();
                actual.OnComplete();
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
