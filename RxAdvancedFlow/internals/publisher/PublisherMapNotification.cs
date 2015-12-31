using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherMapNotification<T, R> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<IPublisher<R>> actual;

        readonly Func<T, IPublisher<R>> onNextCall;

        readonly Func<Exception, IPublisher<R>> onErrorCall;

        readonly Func<IPublisher<R>> onCompleteCall;

        long requested;

        bool done;

        ISubscription s;

        IPublisher<R> last;

        public PublisherMapNotification(ISubscriber<IPublisher<R>> actual,
            Func<T, IPublisher<R>> onNextCall,
            Func<Exception, IPublisher<R>> onErrorCall,
            Func<IPublisher<R>> onCompleteCall
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

            IPublisher<R> p;

            try
            {
                p = onCompleteCall();
            }
            catch (Exception e)
            {
                actual.OnError(e);
                return;
            }

            if (p == null)
            {
                actual.OnError(new NullReferenceException("The onCompleteCall returned a null Publisher"));
                return;
            }

            last = p;
            BackpressureHelper.ScalarPostComplete(ref requested, p, actual);
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                return;
            }

            IPublisher<R> p;

            try
            {
                p = onErrorCall(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            if (p == null)
            {
                actual.OnError(new NullReferenceException("The onCompleteCall returned a null Publisher"));
                return;
            }

            last = p;
            BackpressureHelper.ScalarPostComplete(ref requested, p, actual);
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            IPublisher<R> p;

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

            if (p == null)
            {
                done = true;
                s.Cancel();

                actual.OnError(new NullReferenceException("The onCompleteCall returned a null Publisher"));
                return;
            }

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
                if (BackpressureHelper.ScalarPostCompleteRequest(ref requested, n, last, actual))
                {
                    s.Request(n);
                }
            }
        }
    }
}
