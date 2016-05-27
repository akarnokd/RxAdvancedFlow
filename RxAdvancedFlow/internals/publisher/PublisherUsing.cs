using Reactive.Streams;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherUsing<T, S> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly S resource;

        readonly Action<S> resourceDisposer;

        readonly bool eager;

        ISubscription s;

        int once;

        public PublisherUsing(ISubscriber<T> actual, S resource, Action<S> resourceDisposer, bool eager)
        {
            this.actual = actual;
            this.resource = resource;
            this.resourceDisposer = resourceDisposer;
            this.eager = eager;
        }

        public void Request(long n)
        {
            s.Request(n);
        }

        public void Cancel()
        {
            s.Cancel();

            PostCleanup();
        }

        void PostCleanup()
        {
            try
            {
                PreCleanup();
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        void PreCleanup()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                resourceDisposer(resource);
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void OnNext(T t)
        {
            actual.OnNext(t);
        }

        public void OnError(Exception e)
        {
            if (eager)
            {
                try
                {
                    PreCleanup();
                }
                catch (Exception ex)
                {
                    e = new AggregateException(e, ex);
                }
            }
            actual.OnError(e);

            if (!eager)
            {
                PostCleanup();
            }
        }

        public void OnComplete()
        {
            if (eager)
            {
                try
                {
                    PreCleanup();
                }
                catch (Exception ex)
                {
                    actual.OnError(ex);
                    return;
                }
            }
            actual.OnComplete();

            if (!eager)
            {
                PostCleanup();
            }
        }
    }
}
