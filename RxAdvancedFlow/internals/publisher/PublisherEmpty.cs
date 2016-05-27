using System;
using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherEmpty<T> : IPublisher<T>
    {
        internal static readonly PublisherEmpty<T> Instance = new PublisherEmpty<T>();

        private PublisherEmpty()
        {

        }

        public void Subscribe(ISubscriber<T> s)
        {
            EmptySubscription.Complete(s);
        }
    }
}
