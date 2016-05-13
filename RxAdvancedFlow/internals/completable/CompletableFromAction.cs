using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableFromAction : ICompletable
    {
        readonly Action<ICompletableSubscriber> onSubscribe;

        public CompletableFromAction(Action<ICompletableSubscriber> onSubscribe)
        {
            this.onSubscribe = onSubscribe;
        }

        public void Subscribe(ICompletableSubscriber s)
        {
            try
            {
                onSubscribe(s);
            }
            catch (Exception e)
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }
    }
}
