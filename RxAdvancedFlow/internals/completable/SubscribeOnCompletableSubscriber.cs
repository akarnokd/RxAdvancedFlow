using RxAdvancedFlow.disposables;
using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class SubscribeOnCompletableSubscriber : ICompletableSubscriber
    {
        readonly MultipleAssignmentDisposable mad;

        readonly ICompletableSubscriber actual;

        public SubscribeOnCompletableSubscriber(ICompletableSubscriber actual, MultipleAssignmentDisposable mad)
        {
            this.actual = actual;
            this.mad = mad;
        }

        public void OnSubscribe(IDisposable d)
        {
            mad.Set(d);
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }
    }
}
