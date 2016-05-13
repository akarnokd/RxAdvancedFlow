using System;
using System.Threading;

namespace RxAdvancedFlow.processors
{
    /// <summary>
    /// Represents a hot Completable instance which is also a CompletableSubscriber and
    /// let's one imperatively terminate this instance and relay the termination event to
    /// current and future CompletableSubscribers.
    /// </summary>
    public sealed class CompletableProcessor : ICompletableProcessor
    {

        CompletableInnerDisposable[] subscribers;

        static readonly CompletableInnerDisposable[] Empty = new CompletableInnerDisposable[0];

        static readonly CompletableInnerDisposable[] Terminated = new CompletableInnerDisposable[0];

        Exception error;

        public void OnComplete()
        {
            CompletableInnerDisposable[] a = Interlocked.Exchange(ref subscribers, Terminated);
            foreach (CompletableInnerDisposable c in a)
            {
                c.actual.OnComplete();
            }
        }

        public void OnError(Exception e)
        {
            error = e;
            CompletableInnerDisposable[] a = Interlocked.Exchange(ref subscribers, Terminated);
            foreach (CompletableInnerDisposable c in a)
            {
                c.actual.OnError(e);
            }
        }

        public void OnSubscribe(IDisposable d)
        {
            if (Volatile.Read(ref subscribers) == Terminated)
            {
                d.Dispose();
            }
        }

        public void Subscribe(ICompletableSubscriber s)
        {
            CompletableInnerDisposable cid = new CompletableInnerDisposable(s, this);
            s.OnSubscribe(cid);

            if (Add(cid))
            {
                if (cid.IsDisposed())
                {
                    Remove(cid);
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

        void Remove(CompletableInnerDisposable cid)
        {
            for (;;)
            {
                CompletableInnerDisposable[] a = Volatile.Read(ref subscribers);

                if (a == Terminated)
                {
                    return;
                }

                int n = a.Length;
                int j = -1;

                for (int i = 0; i < n; i++)
                {
                    if (a[i] == cid)
                    {
                        j = i;
                        break;
                    }
                }

                if (j < 0)
                {
                    return;
                }

                CompletableInnerDisposable[] b;

                if (n == 1)
                {
                    b = Empty;
                }
                else
                {
                    b = new CompletableInnerDisposable[n - 1];

                    Array.Copy(a, 0, b, 0, j);
                    Array.Copy(a, j + 1, b, j, n - j - 1);
                }

                if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                {
                    return;
                }
            }
        }

        bool Add(CompletableInnerDisposable cid)
        {
            for (;;)
            {
                CompletableInnerDisposable[] a = Volatile.Read(ref subscribers);

                if (a == Terminated)
                {
                    return false;
                }

                int n = a.Length;
                CompletableInnerDisposable[] b = new CompletableInnerDisposable[n + 1];

                Array.Copy(a, 0, b, 0, n);
                b[n] = cid;
                
                if (Interlocked.CompareExchange(ref subscribers, b, a) == a)
                {
                    return true;
                }    
            }
        }

        sealed class CompletableInnerDisposable : IDisposable
        {
            internal readonly ICompletableSubscriber actual;
            readonly CompletableProcessor parent;

            int disposed;

            public CompletableInnerDisposable(ICompletableSubscriber actual, CompletableProcessor parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref disposed, 1, 0) == 0)
                {
                    parent.Remove(this);
                }
            }

            public bool IsDisposed()
            {
                return Volatile.Read(ref disposed) != 0;
            }
        }
    }
}
