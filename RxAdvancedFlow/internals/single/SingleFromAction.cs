using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class SingleFromAction<T> : ISingle<T>
    {
        readonly Action<ISingleSubscriber<T>> action;

        public SingleFromAction(Action<ISingleSubscriber<T>> action)
        {
            this.action = action;
        }

        public void Subscribe(ISingleSubscriber<T> s)
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
