using Reactive.Streams;
using RxAdvancedFlow.internals.subscriptions;
using System;

namespace RxAdvancedFlow.subscribers
{
    /// <summary>
    /// An ISubscriber implementation that delegates onXXX events to lambdas.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class LambdaSubscriber<T> : ISubscriber<T>, IDisposable
    {
        readonly Action<ISubscription> onSubscribeCall;

        readonly Action<T> onNextCall;

        readonly Action<Exception> onErrorCall;

        readonly Action onCompleteCall;

        ISubscription s;

        bool done;

        public LambdaSubscriber(
            Action<T> onNextCall)
        {
            this.onSubscribeCall = s => s.Request(long.MaxValue);
            this.onNextCall = onNextCall;
            this.onErrorCall = RxAdvancedFlowPlugins.OnError;
            this.onCompleteCall = () => { };
        }


        public LambdaSubscriber(
            Action<T> onNextCall,
            Action<Exception> onErrorCall)
        {
            this.onSubscribeCall = s => s.Request(long.MaxValue);
            this.onNextCall = onNextCall;
            this.onErrorCall = onErrorCall;
            this.onCompleteCall = () => { };
        }

        public LambdaSubscriber(
            Action<T> onNextCall,
            Action<Exception> onErrorCall,
            Action onCompleteCall)
        {
            this.onSubscribeCall = s => s.Request(long.MaxValue);
            this.onNextCall = onNextCall;
            this.onErrorCall = onErrorCall;
            this.onCompleteCall = onCompleteCall;
        }

        public LambdaSubscriber(Action<ISubscription> onSubscribeCall, 
            Action<T> onNextCall, 
            Action<Exception> onErrorCall,
            Action onCompleteCall)
        {
            this.onSubscribeCall = onSubscribeCall;
            this.onNextCall = onNextCall;
            this.onErrorCall = onErrorCall;
            this.onCompleteCall = onCompleteCall; 
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                try
                {
                    onSubscribeCall(s);
                }
                catch (Exception e)
                {
                    done = true;

                    s.Cancel();

                    RxAdvancedFlowPlugins.OnError(e);
                }
            }
        }

        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            try
            {
                onNextCall(t);
            }
            catch (Exception e)
            {
                s.Cancel();

                OnError(e);
            }
        }

        public void OnError(Exception e)
        {
            if (done)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }
            done = true;

            try
            {
                onErrorCall(e);
            }
            catch (Exception ex)
            {
                RxAdvancedFlowPlugins.OnError(new AggregateException(e, ex));
            }
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            
            try
            {
                onCompleteCall();
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void Dispose()
        {
            SubscriptionHelper.Terminate(ref s);
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }
    }
}
