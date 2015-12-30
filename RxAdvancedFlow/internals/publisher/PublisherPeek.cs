using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherPeek<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly Action<ISubscription> onSubscribeCall;

        readonly Action<T> onNextCall;

        readonly Action<Exception> onErrorCall;

        readonly Action onCompleteCall;

        readonly Action onAfterTerminateCall;

        readonly Action<long> onRequestCall;

        readonly Action onCancelCall;

        ISubscription s;

        public PublisherPeek(ISubscriber<T> actual,
            Action<ISubscription> onSubscribeCall,
            Action<T> onNextCall,
            Action<Exception> onErrorCall,
            Action onCompleteCall,
            Action onAfterTerminateCall,
            Action<long> onRequestCall,
            Action onCancelCall
            )
        {
            this.actual = actual;
            this.onSubscribeCall = onSubscribeCall;
            this.onNextCall = onNextCall;
            this.onErrorCall = onErrorCall;
            this.onCompleteCall = onCompleteCall;
            this.onAfterTerminateCall = onAfterTerminateCall;
            this.onRequestCall = onRequestCall;
            this.onCancelCall = onCancelCall;
        }

        public void Cancel()
        {
            try
            {
                onCancelCall?.Invoke();
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }

            s.Cancel();
        }

        public void OnComplete()
        {
            try
            {
                onCompleteCall?.Invoke();
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }

            actual.OnComplete();

            try
            {
                onAfterTerminateCall?.Invoke();
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
                onErrorCall?.Invoke(e);
            }
            catch (Exception ex)
            {
                RxAdvancedFlowPlugins.OnError(ex);
            }

            actual.OnError(e);

            try
            {
                onAfterTerminateCall?.Invoke();
            }
            catch (Exception ex)
            {
                RxAdvancedFlowPlugins.OnError(ex);
            }

        }

        public void OnNext(T t)
        {
            try
            {
                onNextCall?.Invoke(t);
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }

            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            try
            {
                onSubscribeCall?.Invoke(s);
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }

            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            try
            {
                onRequestCall?.Invoke(n);
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }

            s.Request(n);
        }
    }
}
