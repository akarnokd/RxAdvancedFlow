using RxAdvancedFlow.internals.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.single
{
    sealed class SingleCache<T> : ISingle<T>, ISingleSubscriber<T>, IDisposable
    {

        readonly ISingle<T> source;

        ValueOrError result;

        int once;

        IDisposable d;

        SingleDisposable[] subscribers;

        static readonly SingleDisposable[] Empty = new SingleDisposable[0];

        static readonly SingleDisposable[] Terminated = new SingleDisposable[0];

        public SingleCache(ISingle<T> source)
        {
            this.source = source;
            this.subscribers = Empty;
        }

        public void Subscribe(ISingleSubscriber<T> s)
        {
            SingleDisposable sd = new SingleDisposable(this, s);

            s.OnSubscribe(sd);

            if (Add(sd))
            {
                if (sd.IsDisposed())
                {
                    Remove(sd);
                }

                if (Volatile.Read(ref once) == 0)
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        source.Subscribe(this);
                    }
                }
            }
            else
            {
                ValueOrError r = Volatile.Read(ref result);
                if (r.IsError())
                {
                    s.OnError(r.error);
                }
                else
                {
                    s.OnSuccess(r.value);
                }
            }
        }

        internal bool Add(SingleDisposable sd)
        {
            return ProcessorHelper.Add(ref subscribers, sd, Terminated);
        }

        internal bool Remove(SingleDisposable sd)
        {
            return ProcessorHelper.Remove(ref subscribers, sd, Terminated, Empty);
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
            ValueOrError ve = new ValueOrError(t, null);

            if (Interlocked.CompareExchange(ref result, ve, null) == null)
            {
                SingleDisposable[] a = Volatile.Read(ref subscribers);
                if (a != Terminated)
                {
                    a = Interlocked.Exchange(ref subscribers, Terminated);
                    if (a != Terminated)
                    {
                        foreach (SingleDisposable sd in a)
                        {
                            sd.actual.OnSuccess(t);
                        }
                    }
                }

            }
        }

        public void OnError(Exception e)
        {
            ValueOrError ve = new ValueOrError(default(T), e);

            if (Interlocked.CompareExchange(ref result, ve, null) == null)
            {

                SingleDisposable[] a = Volatile.Read(ref subscribers);
                if (a != Terminated)
                {
                    a = Interlocked.Exchange(ref subscribers, Terminated);
                    if (a != Terminated)
                    {
                        foreach (SingleDisposable sd in a)
                        {
                            sd.actual.OnError(e);
                        }
                    }
                }

            }
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);

            OnError(Single.NoSuchElementException());
        }

        internal sealed class SingleDisposable : IDisposable
        {
            readonly SingleCache<T> parent;

            internal readonly ISingleSubscriber<T> actual;

            int once;

            public SingleDisposable(SingleCache<T> parent, ISingleSubscriber<T> actual)
            {
                this.parent = parent;
                this.actual = actual;
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    parent.Remove(this);
                }
            }

            public bool IsDisposed()
            {
                return once != 0;
            }
        }

        sealed class ValueOrError
        {
            internal readonly T value;
            internal readonly Exception error;

            public ValueOrError(T value, Exception error)
            {
                this.value = value;
                this.error = error;
            }

            internal bool IsError()
            {
                return error != null;
            }
        }
    }
}
