using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using RxAdvancedFlow.internals.subscriptions;
using System.Collections.Concurrent;
using System.Threading;
using RxAdvancedFlow.internals.queues;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherToEnumerable<T> : IEnumerable<T>
    {
        readonly IPublisher<T> source;

        readonly int prefetch;

        public PublisherToEnumerable(IPublisher<T> source, int prefetch)
        {
            this.source = source;
            this.prefetch = prefetch;
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        sealed class PublisherToEnumerableInner : ISubscriber<T>, IEnumerator<T>
        {
            readonly int prefetch;

            readonly int limit;

            int produced;

            ISubscription s;

            SpscArrayQueueStruct<T> q;

            T value;

            Exception error;
            bool done;

            public T Current
            {
                get
                {
                    return value;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return value;
                }
            }

            public PublisherToEnumerableInner(int prefetch)
            {
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.q.Init(prefetch);
            }

            public void Dispose()
            {
                SubscriptionHelper.Terminate(ref s);
            }

            public bool MoveNext()
            {
                for (;;)
                {
                    bool d = Volatile.Read(ref done);

                    bool empty = q.IsEmpty();

                    if (d)
                    {
                        Exception e = error;
                        if (e != null)
                        {
                            throw e;
                        }
                        else
                        if (empty)
                        {
                            return false;
                        }
                    }

                    if (!empty)
                    {
                        q.Poll(out value);

                        int p = produced + 1;
                        
                        if (p == limit)
                        {
                            produced = 0;
                            s.Request(p);
                        }
                        else
                        {
                            produced = p;
                        }

                        return true;
                    }

                    Await();
                }
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Signal();
            }

            public void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                Signal();
            }

            public void OnNext(T t)
            {
                q.Offer(t);
                Signal();
            }

            void Await()
            {
                Monitor.Enter(this);
                try
                {
                    while (!Volatile.Read(ref done) && q.IsEmpty())
                    {
                        Monitor.Wait(this);
                    }
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }

            void Signal()
            {
                Monitor.Enter(this);
                try
                {
                    Monitor.PulseAll(this);
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(prefetch);
                }
            }

            public void Reset()
            {
                throw new InvalidOperationException();
            }
        }
    }
}
