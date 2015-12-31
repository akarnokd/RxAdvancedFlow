using ReactiveStreamsCS;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherRedoWhen<T> : ISubscriber<T>, ISubscription
    {
        internal static readonly object NEXT = new object();

        readonly ISubscriber<T> actual;

        readonly Action signalComplete;

        readonly Action<Exception> signalError;

        readonly Action terminateRegular;

        readonly IPublisher<T> source;

        readonly bool errorMode;

        MultiArbiterStruct arbiter;

        long produced;

        int wip;

        public PublisherRedoWhen(ISubscriber<T> actual, IPublisher<T> source, 
            Action signalComplete, Action<Exception> signalError, Action terminateRegular, bool errorMode)
        {
            this.actual = actual;
            this.source = source;
            this.signalError = signalError;
            this.signalComplete = signalComplete;
            this.terminateRegular = terminateRegular;
            this.errorMode = errorMode;
        }

        public void Cancel()
        {
            arbiter.Cancel();
        }

        public void OnComplete()
        {
            if (errorMode)
            {
                terminateRegular();

                actual.OnComplete();
            }
            else
            {
                long p = produced;
                if (p != 0L)
                {
                    produced = 0L;
                    arbiter.Produced(p);
                }

                signalComplete();
            }
        }

        public void OnError(Exception e)
        {
            if (errorMode)
            {
                long p = produced;
                if (p != 0L)
                {
                    produced = 0L;
                    arbiter.Produced(p);
                }

                signalError(e);
            }
            else
            {
                terminateRegular();

                actual.OnError(e);
            }
        }

        public void OnNext(T t)
        {
            produced++;

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

        internal void Resubscribe()
        {
            if (Interlocked.Increment(ref wip) == 1)
            {
                do
                {
                    source.Subscribe(this);
                }
                while (Interlocked.Decrement(ref wip) != 0);
            }
        }
    }

    sealed class PublisherRedoSignaller<T, U> : ISubscriber<U>, ISubscription
    {
        readonly ISubscriber<T> actual;

        Action resubscribe;

        SingleArbiterStruct arbiter;

        public PublisherRedoSignaller(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void SetResubscribe(Action resubscribe)
        {
            this.resubscribe = resubscribe;
        }

        public void Cancel()
        {
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

        public void OnNext(U t)
        {
            resubscribe();
        }

        public void OnSubscribe(ISubscription s)
        {
            arbiter.Set(s);
        }

        public void Request(long n)
        {
            arbiter.Request(n);
        }
    }
}
