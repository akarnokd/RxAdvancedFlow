using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherLift<T, R> : IPublisher<R>
    {
        readonly IPublisher<T> source;

        readonly Func<ISubscriber<R>, ISubscriber<T>> onLift;

        public PublisherLift(IPublisher<T> source, Func<ISubscriber<R>, ISubscriber<T>> onLift)
        {
            this.source = source;
            this.onLift = onLift;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            ISubscriber<T> sr;

            try
            {
                sr = onLift(s);
            }
            catch (Exception ex)
            {
                EmptySubscription.Error(s, ex);
                return;
            }

            source.Subscribe(sr);
        }
    }
}
