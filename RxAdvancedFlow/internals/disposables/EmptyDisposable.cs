using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.disposables
{
    static class EmptyDisposable
    {
        sealed class EmptyDisposableImpl : IDisposable
        {
            public void Dispose()
            {
            }
        }

        public static readonly IDisposable Empty = new EmptyDisposableImpl();

        public static void Error(ICompletableSubscriber cs, Exception e)
        {
            cs.OnSubscribe(Empty);
            cs.OnError(e);
        }


        public static void Complete(ICompletableSubscriber cs)
        {
            cs.OnSubscribe(Empty);
            cs.OnComplete();
        }

        public static void Error<T>(ISingleSubscriber<T> cs, Exception e)
        {
            cs.OnSubscribe(Empty);
            cs.OnError(e);
        }
    }
}
