using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableAndThenSingle<T> : ISingle<T>
    {
        readonly ICompletable first;

        readonly ISingle<T> second;

        public CompletableAndThenSingle(ICompletable first, ISingle<T> second)
        {
            this.first = first;
            this.second = second;
        }

        public void Subscribe(ISingleSubscriber<T> s)
        {
            first.Subscribe(new FirstSubscriber(s, second));
        }

        sealed class FirstSubscriber : ICompletableSubscriber, ISingleSubscriber<T>, IDisposable
        {
            readonly ISingleSubscriber<T> actual;

            readonly ISingle<T> second;

            IDisposable d;

            public FirstSubscriber(ISingleSubscriber<T> actual, ISingle<T> second)
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
                second.Subscribe(this);
            }

            public void OnSuccess(T t)
            {
                actual.OnSuccess(t);
            }

            public void Dispose()
            {
                DisposableHelper.Terminate(ref d);
            }
        }
    }
}
