using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class ResumeNextSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingleSubscriber<T> actual;

        readonly Func<Exception, ISingle<T>> resumeWith;

        bool once;

        IDisposable d;

        public ResumeNextSingleSubscriber(ISingleSubscriber<T> actual, Func<Exception, ISingle<T>> resumeWith)
        {
            this.actual = actual;
            this.resumeWith = resumeWith;
        }

        public void OnSubscribe(IDisposable d)
        {
            DisposableHelper.Replace(ref this.d, d);
        }

        public void OnSuccess(T t)
        {
            actual.OnSuccess(t);
        }

        public void OnError(Exception e)
        {
            if (!once)
            {
                ISingle<T> s;

                try {
                    s = resumeWith(e);
                }
                catch (Exception ex)
                {
                    actual.OnError(new AggregateException(e, ex));
                    return;
                }

                once = true;
                s.Subscribe(this);
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
