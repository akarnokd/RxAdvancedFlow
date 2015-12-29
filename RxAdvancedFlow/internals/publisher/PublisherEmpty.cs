using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
