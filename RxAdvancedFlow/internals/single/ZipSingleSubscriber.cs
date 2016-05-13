using RxAdvancedFlow.disposables;
using System;
using System.Threading;

namespace RxAdvancedFlow.internals.single
{
    sealed class ZipSingleSubscriber<T, R> : ISingleSubscriber<T>
    {
        readonly int index;
        readonly T[] array;
        readonly int[] counter;
        readonly ISingleSubscriber<R> actual;
        readonly ICompositeDisposable all;
        readonly Func<T[], R> zipper;

        public ZipSingleSubscriber(ISingleSubscriber<R> actual,
            int index, T[] array, int[] counter, ICompositeDisposable all,
            Func<T[], R> zipper)
        {
            this.actual = actual;
            this.index = index;
            this.array = array;
            this.counter = counter;
            this.all = all;
            this.zipper = zipper;
        }

        public void OnSubscribe(IDisposable d)
        {
            all.Add(d);
        }

        public void OnSuccess(T t)
        {
            T[] a = array;
            a[index] = t;
            if (Interlocked.Decrement(ref counter[0]) == 0)
            {
                R r;

                try
                {
                    r = zipper(a);
                }
                catch (Exception ex)
                {
                    actual.OnError(ex);
                    return;
                }
                actual.OnSuccess(r);
            }
        }

        public void OnError(Exception e)
        {
            if (Interlocked.Exchange(ref counter[0], 0) > 0)
            {
                actual.OnError(e);
            }
        }
    }
}
