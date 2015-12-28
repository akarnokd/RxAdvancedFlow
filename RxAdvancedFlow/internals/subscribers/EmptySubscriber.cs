using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscribers
{
    /// <summary>
    /// This ISubscriber implementation does nothing.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    sealed class EmptySubscriber<T> : ISubscriber<T>
    {
        public void OnComplete()
        {
        }

        public void OnError(Exception e)
        {
        }

        public void OnNext(T t)
        {
        }

        public void OnSubscribe(ISubscription s)
        {
        }
    }
}
