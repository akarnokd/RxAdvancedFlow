using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableAndThenObservable<T> : IObservable<T>
    {
        readonly ICompletable first;

        readonly IObservable<T> second;

        public CompletableAndThenObservable(ICompletable first, IObservable<T> second)
        {
            this.first = first;
            this.second = second;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            InnerSubscriber inner = new InnerSubscriber(observer, second);

            first.Subscribe(inner);

            return inner;
        }

        sealed class InnerSubscriber : ICompletableSubscriber, IObserver<T>, IDisposable
        {
            readonly IObserver<T> actual;

            readonly IObservable<T> second;

            internal IDisposable d;

            public InnerSubscriber(IObserver<T> actual, IObservable<T> second)
            {
                this.actual = actual;
                this.second = second;
            }

            public void OnSubscribe(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnComplete()
            {
                IDisposable a = second.Subscribe(this);

                DisposableHelper.Replace(ref d, a); 
            }

            public void OnNext(T value)
            {
                actual.OnNext(value);
            }

            public void OnCompleted()
            {
                actual.OnCompleted();
            }

            public void Dispose()
            {
                DisposableHelper.Terminate(ref d);
            }
        }
    }
}
