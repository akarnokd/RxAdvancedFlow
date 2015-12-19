using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.completable
{
    sealed class ResumeCompletableSubscriber : ICompletableSubscriber, IDisposable
    {
        readonly ICompletableSubscriber actual;

        readonly Func<Exception, ICompletable> nextProvider;

        bool once;

        IDisposable d;

        public ResumeCompletableSubscriber(ICompletableSubscriber actual, Func<Exception, ICompletable> nextProvider)
        {
            this.actual = actual;
            this.nextProvider = nextProvider;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {

            if (once)
            {
                actual.OnError(e);
                return;
            }

            once = true;

            ICompletable next;

            try
            {
                next = nextProvider(e);
            }
            catch (Exception ex)
            {
                actual.OnError(new AggregateException(e, ex));
                return;
            }

            next.Subscribe(this);
        }

        public void OnSubscribe(IDisposable d)
        {
            DisposableHelper.Replace(ref this.d, d);
        }
    }
}
