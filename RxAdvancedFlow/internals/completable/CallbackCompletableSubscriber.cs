using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CallbackCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly Action onCompleteCall;

        readonly Action<Exception> onErrorCall;

        IDisposable d;

        public CallbackCompletableSubscriber(Action onCompleteCall, Action<Exception> onErrorCall)
        {
            this.onCompleteCall = onCompleteCall;
            this.onErrorCall = onErrorCall;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void OnComplete()
        {
            try
            {
                onCompleteCall();
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
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

        public void OnSubscribe(IDisposable d)
        {
            if (!DisposableHelper.SetOnce(ref this.d, d))
            {
                d?.Dispose();
                OnSubscribeHelper.ReportDisposableSet();
            }
        }
    }
}
