using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherFromAction<T> : IPublisher<T>
    {
        readonly Action<ISubscriber<T>> action;

        public PublisherFromAction(Action<ISubscriber<T>> action)
        {
            this.action = action;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            try
            {
                action(s);
            }
            catch (Exception ex)
            {
                RxAdvancedFlowPlugins.OnError(ex);
            }
        }
    }
}
