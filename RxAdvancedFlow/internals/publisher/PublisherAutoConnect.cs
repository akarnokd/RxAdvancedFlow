using Reactive.Streams;
using System;
using System.Threading;

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

        public void Subscribe(ISubscriber subscriber)
        {
            throw new NotImplementedException();
        }
    }
}
