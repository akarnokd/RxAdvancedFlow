using ReactiveStreamsCS;
using RxAdvancedFlow.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class EqualsSingleSubscriber<T> : ISingleSubscriber<T>
    {
        readonly int index;
        readonly T[] array;
        readonly int[] counter;
        readonly ISingleSubscriber<bool> actual;
        readonly ICompositeDisposable all;

        public EqualsSingleSubscriber(ISingleSubscriber<bool> actual,
            int index, T[] array, int[] counter, ICompositeDisposable all)
        {
            this.actual = actual;
            this.index = index;
            this.array = array;
            this.counter = counter;
            this.all = all;
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
                for (int i = 0; i < a.Length - 1; i++)
                {
                    if (!a[i].Equals(a[i + 1]))
                    {
                        actual.OnSuccess(false);
                        return;
                    }
                }

                actual.OnSuccess(true);
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
