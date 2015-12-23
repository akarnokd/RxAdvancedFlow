using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherMapSubscriber<T, R> : ISubscriber<T>
    {
        readonly ISubscriber<R> actual;

        readonly Func<T, R> mapper;

        ISubscription s;

        bool done;

        public PublisherMapSubscriber(ISubscriber<R> actual, Func<T, R> mapper)
        {
            this.actual = actual;
            this.mapper = mapper;
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            done = true;

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                return;
            }
            done = true;
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            R r;

            try
            {
                r = mapper(t);
            }
            catch (Exception ex)
            {
                s.Cancel();
                OnError(ex);
                return;
            }

            actual.OnNext(r);
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
