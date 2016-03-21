using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherObserveOn<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        readonly bool delayError;

        readonly IWorker worker;

        ISubscription s;

        SpscArrayQueueStruct<T> q;

        BasicBackpressureStruct bp;

        bool done;
        Exception error;

        public PublisherObserveOn(ISubscriber<T> actual, IWorker worker, bool delayError, int bufferSize)
        {
            this.actual = actual;
            this.delayError = delayError;
            this.q.Init(bufferSize);
            this.worker = worker;
        }

        public void Cancel()
        {
            bp.Cancel();
            s.Cancel();
            worker.Dispose();

            if (bp.Enter())
            {
                q.Clear();
            }
        }

        public void OnComplete()
        {
            Volatile.Write(ref done, true);
            Schedule();
        }

        public void OnError(Exception e)
        {
            error = e;
            Volatile.Write(ref done, true);
            Schedule();
        }

        public void OnNext(T t)
        {
            if (!q.Offer(t))
            {
                s.Cancel();

                OnError(BackpressureHelper.MissingBackpressureException());
                return;
            }
            else
            {
                Schedule();
            } 
        }

        void Schedule()
        {
            if (bp.Enter())
            {
                worker.Schedule(this.Drain);
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                bp.Request(n);

                Schedule();
            }
        }

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        void Drain()
        {
            int missed = 1;
            ISubscriber<T> a = actual; 

            for (;;)
            {
                if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                long r = bp.Requested();
                long e = 0L;

                while (r != e)
                {
                    bool d = IsDone();

                    T t;
                    bool empty = !q.Poll(out t);

                    if (CheckTerminated(d, empty, a))
                    {
                        return;
                    }

                    if (empty)
                    {
                        break;
                    }

                    a.OnNext(t);

                    e++;
                }

                if (e == r && CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                if (e != 0L)
                {
                    if (r != long.MaxValue)
                    {
                        bp.Produced(e);
                    }
                    s.Request(e);
                }

                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        bool CheckTerminated(bool d, bool empty, ISubscriber<T> a)
        {
            if (bp.IsCancelled())
            {
                q.Clear();
                return true;
            }

            if (d)
            {
                if (delayError)
                {
                    if (empty)
                    {
                        worker.Dispose();

                        Exception e = error;
                        if (e != null)
                        {
                            a.OnError(e);
                        }
                        else
                        {
                            a.OnComplete();
                        }
                        return true;
                    }
                }
                else
                {
                    Exception e = error;
                    if (e != null)
                    {
                        q.Clear();
                        worker.Dispose();

                        a.OnError(e);
                        return true;
                    }
                    else
                    if (empty)
                    {
                        worker.Dispose();

                        a.OnComplete();
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
