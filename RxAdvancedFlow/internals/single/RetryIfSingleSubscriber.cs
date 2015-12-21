using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class RetryIfSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ISingle<T> source;

        readonly ISingleSubscriber<T> actual;

        readonly Func<Exception, bool> retryIf;

        IDisposable d;

        int wip;

        public RetryIfSingleSubscriber(ISingle<T> source, ISingleSubscriber<T> actual, Func<Exception, bool> retryIf)
        {
            this.source = source;
            this.actual = actual;
            this.retryIf = retryIf;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
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
            bool b;

            try
            {
                b = retryIf(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            if (b)
            {
                Resubscribe();
            }
        }

        internal void Resubscribe()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                do
                {
                    
                    source.Subscribe(this);

                } while (Interlocked.Decrement(ref wip) != 0);
            }
        }
    }
}
