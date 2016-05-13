using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class FlatMapSingleSubscriber<T, R> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingleSubscriber<R> actual;

        readonly Func<T, ISingle<R>> mapper;

        IDisposable d;

        public FlatMapSingleSubscriber(ISingleSubscriber<R> actual, Func<T, ISingle<R>> mapper)
        {
            this.actual = actual;
            this.mapper = mapper;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            if (DisposableHelper.SetOnce(ref this.d, d))
            {
                actual.OnSubscribe(this);
            }
            else
            {
                d?.Dispose();
                OnSubscribeHelper.ReportDisposableSet();
            }
        }

        public void OnSuccess(T t)
        {
            ISingle<R> s;

            try
            {
                s = mapper(t);
            }
            catch (Exception e)
            {
                actual.OnError(e);
                return;
            }
            s.Subscribe(new InnerSingleSubscriber(this));
        }

        internal void Set(IDisposable d)
        {
            DisposableHelper.Set(ref this.d, d);
        }

        sealed class InnerSingleSubscriber : ISingleSubscriber<R>
        {
            readonly FlatMapSingleSubscriber<T, R> parent;

            public InnerSingleSubscriber(FlatMapSingleSubscriber<T, R> parent)
            {
                this.parent = parent;
            }

            public void OnError(Exception e)
            {
                parent.actual.OnError(e);
            }

            public void OnSubscribe(IDisposable d)
            {
                parent.Set(d);
            }

            public void OnSuccess(R t)
            {
                parent.actual.OnSuccess(t);
            }
        }
    }
}
