using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class LifecycleSingleSubscriber<T> : ISingleSubscriber<T>
    {
        readonly ISingleSubscriber<T> actual;

        readonly Action<T> onSuccessCall;

        readonly Action<Exception> onErrorCall;

        readonly Action<IDisposable> onSubscribeCall;

        readonly Action onAfterTerminateCall;

        public LifecycleSingleSubscriber(ISingleSubscriber<T> actual,
            Action<IDisposable> onSubscribeCall,
            Action<T> onSuccessCall, 
            Action<Exception> onErrorCall,
            Action onAfterTerminateCall
            )
        {
            this.actual = actual;
            this.onSubscribeCall = onSubscribeCall;
            this.onSuccessCall = onSuccessCall;
            this.onErrorCall = onErrorCall;
            this.onAfterTerminateCall = onAfterTerminateCall;
        }

        public void OnError(Exception e)
        {
            try
            {
                onErrorCall(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            actual.OnError(e);

            DoAfterTerminated();
        }

        void DoAfterTerminated()
        {
            try
            {
                onAfterTerminateCall();
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            try
            {
                onSubscribeCall(d);
            }
            catch (Exception e)
            {
                d.Dispose();
                EmptyDisposable.Error(actual, e);
                return;
            }

            actual.OnSubscribe(d);
        }

        public void OnSuccess(T t)
        {
            try
            {
                onSuccessCall(t);
            }
            catch (Exception e)
            {
                OnError(e);
                return;
            }

            actual.OnSuccess(t);

            DoAfterTerminated();
        }
    }
}
