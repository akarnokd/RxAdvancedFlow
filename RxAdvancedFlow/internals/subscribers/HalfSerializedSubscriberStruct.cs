using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscribers
{
    /// <summary>
    /// Wraps a Subscriber and serialized terminal events with OnNext
    /// events. This can be used to safely stop a sequence of OnNext
    /// events via an OnError or OnComplete.
    /// </summary>
    sealed class HalfSerializedSubscriberStruct<T>
    {
        ISubscriber<T> actual;

        Exception error;

        int wip;

        internal void OnSubscribe(ISubscription s)
        {
            actual.OnSubscribe(s);
        }
        
        internal void OnNext(T value)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                actual.OnNext(value);

                if (Interlocked.Decrement(ref wip) != 0)
                {
                    Terminate();
                }
            }
        }

        internal void OnError(Exception e)
        {
            ExceptionHelper.Add(ref error, e);
            if (Interlocked.Increment(ref wip) == 1)
            {
                Terminate();
            }
        }

        internal void OnComplete()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                Terminate();
            }
        }

        void Terminate()
        {
            Exception e;

            ExceptionHelper.Terminate(ref error, out e);

            if (e != null)
            {
                actual.OnError(e);
            }
            else
            {
                actual.OnComplete();
            }
        }

        internal void Init(ISubscriber<T> actual)
        {
            this.actual = actual;
        }
    }
}
