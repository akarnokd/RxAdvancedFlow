using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscribers
{
    /// <summary>
    /// Wraps an ISubscriber and translates events and lifecycle of IObserver.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    sealed class SubscriberObserver<T> : IObserver<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        IDisposable d;

        public SubscriberObserver(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Set(IDisposable d)
        {
            DisposableHelper.SetOnce(ref this.d, d);
        }

        public void Cancel()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void OnCompleted()
        {
            actual.OnComplete();
        }

        public void OnError(Exception error)
        {
            actual.OnError(error);
        }

        public void OnNext(T value)
        {
            actual.OnNext(value);
        }

        public void Request(long n)
        {
            // ignored at this level
        }
    }
}
