using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class OnErrorCompleteCompletableSubscriber : ICompletableSubscriber
    {
        readonly ICompletableSubscriber actual;

        public OnErrorCompleteCompletableSubscriber(ICompletableSubscriber actual)
        {
            this.actual = actual;
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            actual.OnComplete();
        }

        public void OnSubscribe(IDisposable d)
        {
            actual.OnSubscribe(d);
        }
    }
}
