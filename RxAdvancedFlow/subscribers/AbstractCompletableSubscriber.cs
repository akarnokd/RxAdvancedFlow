using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.subscribers
{
    /// <summary>
    /// Represents a CompletableSubscriber which supports asynchronous call to Dispose().
    /// </summary>
    public abstract class AbstractCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        IDisposable d;

        public abstract void OnComplete();

        public abstract void OnError(Exception e);

        /// <summary>
        /// Override this method to perform activities when the source completable
        /// calls OnSubscribe.
        /// </summary>
        protected virtual void OnStart()
        {

        }

        /// <summary>
        /// Override this method to perform activities once if the Dispose() method is
        /// called.
        /// </summary>
        protected virtual void OnDisposed()
        {

        }

        public void OnSubscribe(IDisposable d)
        {
            if (!DisposableHelper.SetOnce(ref this.d, d))
            {
                d?.Dispose();
                OnSubscribeHelper.ReportDisposableSet();
            }
            else
            {
                OnStart();
            }
        }

        public void Dispose()
        {
            if (DisposableHelper.Terminate(ref d))
            {
                OnDisposed();
            }
        }
    }
}
