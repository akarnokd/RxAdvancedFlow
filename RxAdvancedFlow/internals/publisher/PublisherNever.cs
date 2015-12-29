using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
