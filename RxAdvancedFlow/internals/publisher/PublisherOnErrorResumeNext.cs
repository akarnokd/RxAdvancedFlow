using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;
using System;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherOnErrorResumeNext<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Func<Exception, IPublisher<T>> resumeFunction;

        MultiArbiterStruct arbiter;

        long produced;

        public PublisherOnErrorResumeNext(ISubscriber<T> actual,
            Func<Exception, IPublisher<T>> resumeFunction)
        {
            this.actual = actual;
            this.resumeFunction = resumeFunction;
        }

        public void Cancel()
        {
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            long i = produced;
            if (i != 0L)
            {
                arbiter.Produced(i);
            }

            IPublisher<T> p;

            try
            {
                p = resumeFunction(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            if (p == null)
            {
                actual.OnError(new AggregateException(e, new NullReferenceException("The resumeFunction returned a null Publisher")));
                return;
            }
        }

        public void OnNext(T t)
        {
            produced++;

            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            actual.OnSubscribe(this);

            arbiter.Set(s);
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        void Set(ISubscription s)
        {
            arbiter.Set(s);
        }

        sealed class PublisherOnErrorResumeNextInner : ISubscriber<T>
        {

            readonly PublisherOnErrorResumeNext<T> parent;

            readonly ISubscriber<T> actual;

            public PublisherOnErrorResumeNextInner(PublisherOnErrorResumeNext<T> parent, ISubscriber<T> actual)
            {
                this.parent = parent;
                this.actual = actual;
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
                actual.OnNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.Set(s);
            }
        }
    }
}
