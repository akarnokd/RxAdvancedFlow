using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscribers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.subscribers
{
    /// <summary>
    /// Wraps another ISubscriber and serializes calls to the OnNext, OnError and OnComplete
    /// methods.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class LockedSerializedSubscriber<T> : ISubscriber<T>
    {
        LockedSerializedSubscriberStruct<T> s;

        public LockedSerializedSubscriber(ISubscriber<T> actual)
        {
            s.Init(actual);
        }

        public void OnComplete()
        {
            s.OnComplete();
        }

        public void OnError(Exception e)
        {
            s.OnError(e);
        }

        public void OnNext(T t)
        {
            s.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            this.s.OnSubscribe(new CancelWrapper(this.s.Cancel, s));
        }

        sealed class CancelWrapper : ISubscription
        {
            readonly Action onCancel;

            readonly ISubscription s;

            public CancelWrapper(Action onCancel, ISubscription s)
            {
                this.onCancel = onCancel;
                this.s = s;
            }

            public void Cancel()
            {
                onCancel();
                s.Cancel();
            }

            public void Request(long n)
            {
                s.Request(n);
            }
        }
    }
}
