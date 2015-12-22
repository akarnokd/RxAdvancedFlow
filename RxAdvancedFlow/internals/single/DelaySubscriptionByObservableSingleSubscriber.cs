using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class DelaySubscriptionByObservableSingleSubscriber<T, U>
        : ISingleSubscriber<T>, IObserver<U>, IDisposable
    {
        readonly ISingle<T> source;

        readonly ISingleSubscriber<T> actual;

        bool once;

        IDisposable other;

        IDisposable d;

        public DelaySubscriptionByObservableSingleSubscriber(ISingle<T> source, ISingleSubscriber<T> actual)
        {
            this.source = source;
            this.actual = actual;
        }

        public void Set(IDisposable d)
        {
            DisposableHelper.Replace(ref other, d);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (!DisposableHelper.SetOnce(ref this.d, d))
            {
                d?.Dispose();
                OnSubscribeHelper.ReportDisposableSet();
            }
        }

        public void OnSuccess(T t)
        {
            actual.OnSuccess(t);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(U value)
        {
            if (!once)
            {
                once = true;

                DisposableHelper.Terminate(ref other);

                DoSubscribe();
            }
        }

        public void OnCompleted()
        {
            if (!once)
            {
                once = true;

                DoSubscribe();
            }
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref other);
            DisposableHelper.Terminate(ref d);
        }

        void DoSubscribe()
        {
            source.Subscribe(this);
        }
    }
}
