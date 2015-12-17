using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            
        }
    }
}
