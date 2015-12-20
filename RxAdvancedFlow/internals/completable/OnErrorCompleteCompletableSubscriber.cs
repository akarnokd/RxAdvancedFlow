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

        readonly Func<Exception, bool> predicate;

        public OnErrorCompleteCompletableSubscriber(ICompletableSubscriber actual,
            Func<Exception, bool> predicate)
        {
            this.actual = actual;
            this.predicate = predicate;
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            bool b;

            try
            {
                b = predicate(e);
            } 
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            if (b)
            {
                actual.OnComplete();
            }
            else
            {
                actual.OnError(e);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            actual.OnSubscribe(d);
        }
    }
}
