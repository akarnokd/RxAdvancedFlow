using ReactiveStreamsCS;
using RxAdvancedFlow.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class AmbSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly ICompositeDisposable all;

        readonly ISingleSubscriber<T> actual;

        int once;

        public AmbSingleSubscriber(ISingleSubscriber<T> actual)
        {
            this.actual = actual;
            all = new SetCompositeDisposable();
        }

        public void OnError(Exception e)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                all.Dispose();

                actual.OnError(e);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            all.Add(d);
        }

        public void OnSuccess(T t)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                all.Dispose();

                actual.OnSuccess(t);
            }
        }

        public void Dispose()
        {
            all.Dispose();
        }

        public bool IsDisposed()
        {
            return Volatile.Read(ref once) != 0 || all.IsDisposed();
        }
    }
}
