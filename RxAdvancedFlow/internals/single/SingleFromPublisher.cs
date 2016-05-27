using Reactive.Streams;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class SingleFromPublisher<T> : ISingle<T>
    {
        readonly IPublisher<T> source;

        internal SingleFromPublisher(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISingleSubscriber<T> s)
        {
            source.Subscribe(new SinglePublisherSubscriber(s));
        }

        sealed class SinglePublisherSubscriber : ISubscriber<T>, IDisposable
        {
            readonly ISingleSubscriber<T> actual;

            int count;

            T value;

            ISubscription s;

            internal SinglePublisherSubscriber(ISingleSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void Dispose()
            {
                s.Cancel();
            }

            public void OnComplete()
            {
                if (count == 1)
                {
                    actual.OnSuccess(value);
                }
                else
                if (count == 0) 
                {
                    actual.OnError(new IndexOutOfRangeException("Source is empty"));
                }
            }

            public void OnError(Exception e)
            {
                if (count < 2)
                {
                    actual.OnError(e);
                }
                else
                {
                    RxAdvancedFlowPlugins.OnError(e);
                }
            }

            public void OnNext(T t)
            {
                value = t;
                if (++count == 2)
                {
                    Dispose();

                    actual.OnError(new IndexOutOfRangeException("Source contains more than one element"));
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                if (OnSubscribeHelper.SetSubscription(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }
        }
    }
}
