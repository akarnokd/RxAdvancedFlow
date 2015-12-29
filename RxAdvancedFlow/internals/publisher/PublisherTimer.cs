using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherTimer : ISubscription
    {
        readonly ISubscriber<long> actual;

        IDisposable timer;

        ScalarDelayedSubscriptionStruct<long> sds;

        public PublisherTimer(ISubscriber<long> actual)
        {
            this.actual = actual;
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                sds.Request(n, actual);
            }
        }

        public void Cancel()
        {
            sds.Cancel();
            DisposableHelper.Terminate(ref timer);
        }
        
        public void Set(IDisposable d)
        {
            DisposableHelper.SetOnce(ref timer, d);
        }

        public void Signal()
        {
            sds.Set(0L, actual);
        }
    }
}
