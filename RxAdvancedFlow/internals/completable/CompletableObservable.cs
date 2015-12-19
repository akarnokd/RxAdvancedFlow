using ReactiveStreamsCS;
using RxAdvancedFlow.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableObservable<T> : IObservable<T>
    {
        readonly ICompletable source;

        public CompletableObservable(ICompletable source)
        {
            this.source = source;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            InnerCompletableSubscriber ics = new InnerCompletableSubscriber(observer);

            source.Subscribe(ics);

            return ics;
        }

        sealed class InnerCompletableSubscriber : AbstractCompletableSubscriber
        {
            readonly IObserver<T> actual;

            public InnerCompletableSubscriber(IObserver<T> actual)
            {
                this.actual = actual;
            }

            public override void OnComplete()
            {
                actual.OnCompleted();
            }

            public override void OnError(Exception e)
            {
                actual.OnError(e);
            }
        }
    }
}
