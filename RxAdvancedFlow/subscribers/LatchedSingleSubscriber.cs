using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.subscribers
{
    public sealed class LatchedSingleSubscriber<T> : ISingleSubscriber<T>, IDisposable
    {
        readonly CountdownEvent cdl = new CountdownEvent(1);

        T value;

        Exception error;

        IDisposable d;

        public void OnError(Exception e)
        {
            error = e;
            cdl.Signal();
        }

        public void OnSubscribe(IDisposable d)
        {
            if (!DisposableHelper.SetOnce(ref this.d, d))
            {
                d?.Dispose();
                OnSubscribeHelper.ReportDisposableSet();
            }
        }

        public void OnSuccess(T t)
        {
            value = t;
            cdl.Signal();
        }

        public T Get()
        {
            if (cdl.CurrentCount != 0)
            {
                cdl.Wait();
            }

            Exception e = error;
            if (e != null)
            {
                throw e;
            }
            return value;
        }

        public T Get(TimeSpan timeout)
        {
            if (cdl.CurrentCount != 0)
            {
                if (!cdl.Wait(timeout))
                {
                    throw new TimeoutException();
                }
            }

            Exception e = error;
            if (e != null)
            {
                throw e;
            }
            return value;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public bool IsTerminated()
        {
            return cdl.CurrentCount == 0;
        }
    }
}
