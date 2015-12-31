using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.processors
{
    /// <summary>
    /// A processor implementation that dispatches events to current subscribers.
    /// If a subscriber can't keep up, it will receive a missing backpressure exception.
    /// </summary>
    /// <typeparam name="T">The input and output type.</typeparam>
    public sealed class PublishProcessor<T> : IProcessor<T, T>
    {
        static readonly PublishProcessorInner[] Empty = new PublishProcessorInner[0];
        static readonly PublishProcessorInner[] Terminated = new PublishProcessorInner[0];

        PublishProcessorInner[] subscribers;

        Exception error;

        public void OnSubscribe(ISubscription s)
        {
            if (Volatile.Read(ref subscribers) == Terminated)
            {
                s.Cancel();
            }
        }

        public void OnNext(T value)
        {
            foreach (PublishProcessorInner inner in Volatile.Read(ref subscribers))
            {
                inner.OnNext(value);
            }
        }

        public void OnError(Exception e)
        {
            error = e;
            foreach (PublishProcessorInner inner in ProcessorHelper.Terminate(ref subscribers, Terminated))
            {
                inner.OnError(e);
            }
        }

        public void OnComplete()
        {
            foreach (PublishProcessorInner inner in ProcessorHelper.Terminate(ref subscribers, Terminated))
            {
                inner.OnComplete();
            }
        }

        public void Subscribe(ISubscriber<T> s)
        {
            PublishProcessorInner inner = new PublishProcessorInner(s, this);

            s.OnSubscribe(inner);

            if (Add(inner))
            {
                if (inner.IsCancelled())
                {
                    Remove(inner);
                }
            }
            else
            {
                Exception e = error;
                if (e != null)
                {
                    s.OnError(e);
                }
                else
                {
                    s.OnComplete();
                }
            }
        }

        void Remove(PublishProcessorInner inner)
        {
            ProcessorHelper.Remove(ref subscribers, inner, Terminated, Empty);
        }

        bool Add(PublishProcessorInner inner)
        {
            return ProcessorHelper.Add(ref subscribers, inner, Terminated);
        }

        sealed class PublishProcessorInner : ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly PublishProcessor<T> parent;

            long requested;

            int once;

            public PublishProcessorInner(ISubscriber<T> actual, PublishProcessor<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    parent.Remove(this);
                }
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref once) != 0;
            }

            public void Request(long n)
            {
                if (OnSubscribeHelper.ValidateRequest(n))
                {
                    BackpressureHelper.Add(ref requested, n);
                }
            }

            internal void OnError(Exception e)
            {
                actual.OnError(e);
            }

            internal void OnComplete()
            {
                actual.OnComplete();
            }

            internal void OnNext(T value)
            {
                long r = Volatile.Read(ref requested);
                if (r != 0L)
                {
                    actual.OnNext(value);
                    if (r != long.MaxValue)
                    {
                        Interlocked.Decrement(ref requested);
                    }
                }
                else
                {
                    Cancel();

                    actual.OnError(BackpressureHelper.MissingBackpressureException());
                }
            }
        }
    }
}
