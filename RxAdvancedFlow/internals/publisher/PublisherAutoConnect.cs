using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherAutoConnect<T> : IPublisher<T>
    {
        readonly IConnectablePublisher<T> source;

        readonly Action<IDisposable> onConnect;

        readonly int limit;

        int count;

        public PublisherAutoConnect(IConnectablePublisher<T> source, int limit, Action<IDisposable> onConnect)
        {
            this.source = source;
            this.onConnect = onConnect;
            this.limit = limit;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            source.Subscribe(s);

            if (Interlocked.Increment(ref count) == limit)
            {
                source.Connect(onConnect);
            }
        }
    }
}
