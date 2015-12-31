using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherMapNotification<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<R> actual;

        readonly Func<T, R> onNextCall;

        readonly Func<Exception, R> onErrorCall;

        readonly Func<R> onCompleteCall;

        long requested;

        long produced;

        bool done;

        ISubscription s;

        R last;

        public PublisherMapNotification(
            ISubscriber<R> actual,
            Func<T, R> onNextCall,
            Func<Exception, R> onErrorCall,
            Func<R> onCompleteCall
            )
        {
            this.actual = actual;
            this.onNextCall = onNextCall;
            this.onErrorCall = onErrorCall;
            this.onCompleteCall = onCompleteCall;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }

            R v;

            try
            {
                v = onCompleteCall();
            }
            catch (Exception e)
            {
                actual.OnError(e);
                return;
            }

            BackpressureHelper.Produced(ref requested, produced);

            last = v;
            BackpressureHelper.ScalarPostComplete(ref requested, v, actual);
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                return;
            }

            R v;

            try
            {
                v = onErrorCall(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            BackpressureHelper.Produced(ref requested, produced);

            last = v;
            BackpressureHelper.ScalarPostComplete(ref requested, v, actual);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            R p;

            try
            {
                p = onNextCall(t);
            }
            catch (Exception ex)
            {
                done = true;
                s.Cancel();

                actual.OnError(ex);
                return;
            }

            produced++;

            actual.OnNext(p);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                if (BackpressureHelper.ScalarPostCompleteRequest(ref requested, n, ref last, actual))
                {
                    s.Request(n);
                }
            }
        }
    }
}
