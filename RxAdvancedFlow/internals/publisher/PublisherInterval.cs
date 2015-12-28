using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherInterval : ISubscription
    {
        readonly ISubscriber<long> actual;

        IDisposable timer;

        long count;

        long requested;

        public PublisherInterval(ISubscriber<long> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            DisposableHelper.Terminate(ref timer);
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                BackpressureHelper.Add(ref requested, n);
            }
        }

        public void SetTimer(IDisposable d)
        {
            if (Interlocked.CompareExchange(ref timer, d, null) != null)
            {
                d.Dispose();
            }
        }

        internal void Run()
        {
            long c = count;
            count = c + 1;

            long r = Volatile.Read(ref requested);
            if (r != 0L)
            {
                actual.OnNext(c);

                if (r != long.MaxValue)
                {
                    Interlocked.Decrement(ref requested);
                }
                return;
            }
            Cancel();

            actual.OnError(BackpressureHelper.MissingBackpressureException());
        }
    }
}
