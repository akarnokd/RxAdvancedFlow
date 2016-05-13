using System;

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
