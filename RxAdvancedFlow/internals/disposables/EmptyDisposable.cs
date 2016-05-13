using System;

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

        public static readonly IDisposable Instance = new EmptyDisposableImpl();

        public static void Error(ICompletableSubscriber cs, Exception e)
        {
            cs.OnSubscribe(Instance);
            cs.OnError(e);
        }


        public static void Complete(ICompletableSubscriber cs)
        {
            cs.OnSubscribe(Instance);
            cs.OnComplete();
        }

        public static void Error<T>(ISingleSubscriber<T> cs, Exception e)
        {
            cs.OnSubscribe(Instance);
            cs.OnError(e);
        }
    }
}
