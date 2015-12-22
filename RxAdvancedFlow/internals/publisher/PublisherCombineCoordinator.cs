using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherCombineCoordinator<T, R> : ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Func<T[], R> combiner;

        InnerSubscriber[] subscribers;

        MpscStructLinkedQueue<CombineNode> q;

        public PublisherCombineCoordinator(ISubscriber<T> actual, Func<T[], R> combiner)
        {
            this.actual = actual;
            this.combiner = combiner;
        }
        
        public void Subscribe(IPublisher<T>[] sources, int n)
        {

        }

        public void Cancel()
        {
            throw new NotImplementedException();
        }

        public void Request(long n)
        {
            throw new NotImplementedException();
        }

        sealed class InnerSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly PublisherCombineCoordinator<T> parent;

            ISubscription s;

            long requested;

            public InnerSubscriber(ISubscriber<T> actual, PublisherCombineCoordinator<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void Request(long n)
            {
                throw new NotImplementedException();
            }

            public void Cancel()
            {
                throw new NotImplementedException();
            }

            public void OnSubscribe(ISubscription s)
            {
                throw new NotImplementedException();
            }

            public void OnNext(T t)
            {
                throw new NotImplementedException();
            }

            public void OnError(Exception e)
            {
                throw new NotImplementedException();
            }

            public void OnComplete()
            {
                throw new NotImplementedException();
            }
        }

        internal struct CombineNode
        {
            T value;
            bool done;
            int index;
        }
    }
}
