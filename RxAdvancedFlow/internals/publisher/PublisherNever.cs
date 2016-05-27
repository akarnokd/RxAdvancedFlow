using System;
using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherNever<T> : IPublisher<T>
    {

        internal static readonly PublisherNever<T> Instance = new PublisherNever<T>();

        private PublisherNever()
        {

        }

        public void Subscribe(ISubscriber<T> s)
        {
            s.OnSubscribe(EmptySubscription.Instance);
        }
    }
}
