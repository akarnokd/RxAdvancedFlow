using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletablePublisher<T> : IPublisher<T>
    {
        readonly ICompletable source;

        public CompletablePublisher(ICompletable source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {

            source.Subscribe(new InnerCompletableSubscriber(s));
        }

        sealed class InnerCompletableSubscriber : ICompletableSubscriber
        {
            readonly ISubscriber<T> actual;

            public InnerCompletableSubscriber(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnSubscribe(IDisposable d)
            {
                actual.OnSubscribe(new DisposableSubscription(d));
            }
        }
    }
}
