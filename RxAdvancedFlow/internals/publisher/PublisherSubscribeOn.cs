using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSubscribeOn<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        SingleArbiterStruct arbiter;

        IDisposable d;

        static readonly IDisposable Finished = new FinishedDisposable();

        public PublisherSubscribeOn(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Set(IDisposable d)
        {
            var a = Volatile.Read(ref this.d);

            if (a == DisposableHelper.Disposed)
            {
                d.Dispose();
                return;
            }

            a = Interlocked.CompareExchange(ref this.d, d, a);

            if (a == DisposableHelper.Disposed)
            {
                d.Dispose();
                return;
            }
        }

        public void Clear()
        {
            for (;;)
            {
                var a = Volatile.Read(ref this.d);

                if (a == DisposableHelper.Disposed)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref this.d, Finished, a) == a)
                {
                    return;
                }
            }
        }

        public void Cancel()
        {
            DisposableHelper.Terminate(ref d);
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }

        sealed class FinishedDisposable : IDisposable
        {
            public void Dispose()
            {
                // deliberately no option
            }
        }
    }

    sealed class PublisherSubscribeOnRequest<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly IWorker worker;

        SingleArbiterStruct arbiter;

        public PublisherSubscribeOnRequest(ISubscriber<T> actual, IWorker worker)
        {
            this.actual = actual;
            this.worker = worker;
        }

        public void Cancel()
        {
            arbiter.Cancel();
            worker.Dispose();
        }

        public void OnComplete()
        {
            worker.Dispose();

            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            worker.Dispose();

            actual.OnError(e);
        }

        public void OnNext(T t)
        {
            actual.OnNext(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void Request(long n)
        {
            worker.Schedule(() =>
            {
                arbiter.Request(n);
            });
        }
    }

}
