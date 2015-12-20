using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscribers
{
    sealed class LambdaCompletableSubscriber : ICompletableSubscriber
    {
        readonly ICompletableSubscriber actual;

        readonly Action<IDisposable> onSubscribeCall;

        readonly Action onCompleteCall;

        readonly Action<Exception> onErrorCall;

        readonly Action onAfterTerminate;

        public LambdaCompletableSubscriber(
            ICompletableSubscriber actual,
            Action<IDisposable> onSubscribeCall,
            Action onCompleteCall,
            Action<Exception> onErrorCall,
            Action onAfterTerminate
            )
        {
            this.actual = actual;
            this.onSubscribeCall = onSubscribeCall;
            this.onCompleteCall = onCompleteCall;
            this.onErrorCall = onErrorCall;
            this.onAfterTerminate = onAfterTerminate;
        }

        public void OnComplete()
        {
            try {
                onCompleteCall();
            } catch (Exception e)
            {
                OnError(e);
                return;
            }

            actual.OnComplete();

            try {
                onAfterTerminate();
            } catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnError(Exception e)
        {
            try {
                onErrorCall(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(ex, e));
                return;
            }
            actual.OnError(e);

            try
            {
                onAfterTerminate();
            }
            catch (Exception ex)
            {
                RxAdvancedFlowPlugins.OnError(ex);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            try {
                onSubscribeCall(d);
            } catch (Exception e)
            {
                actual.OnSubscribe(EmptyDisposable.Instance);
                actual.OnError(e);
                return;
            }

            actual.OnSubscribe(d);
        }
    }
}
