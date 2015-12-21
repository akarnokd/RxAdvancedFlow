using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class LambdaSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly Action<T> onSuccessCall;

        readonly Action<Exception> onErrorCall;

        IDisposable d;

        public LambdaSingleSubscriber(Action<T> onSuccessCall, Action<Exception> onErrorCall)
        {
            this.onSuccessCall = onSuccessCall;
            this.onErrorCall = onErrorCall;
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
            try
            {
                onSuccessCall(t);
            }
            catch (Exception e)
            {
                OnError(e);
            }
        }

        public void OnError(Exception e)
        {
            try
            {
                onErrorCall(e);
            }
            catch (Exception ex)
            {
                RxAdvancedFlowPlugins.OnError(new AggregateException(e, ex));
            }
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }
    }
}
