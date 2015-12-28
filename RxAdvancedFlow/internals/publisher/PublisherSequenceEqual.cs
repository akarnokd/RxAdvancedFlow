using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSequenceEqual<T> : ISubscription
    {
        readonly ISubscriber<bool> actual;

        SingleArbiterStruct arbiter;

        public void Cancel()
        {
            throw new NotImplementedException();
        }

        public void Request(long n)
        {
            throw new NotImplementedException();
        }
    }
}
