using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.disposables
{
    sealed class DisposableSubscription : ISubscription
    {
        readonly IDisposable d;

        public DisposableSubscription(IDisposable d)
        {
            this.d = d;
        }

        public void Cancel()
        {
            d.Dispose();
        }

        public void Request(long n)
        {
            // ignored
        }
    }
}
