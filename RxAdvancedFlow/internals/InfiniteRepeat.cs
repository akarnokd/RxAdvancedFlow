using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals
{
    sealed class InfiniteRepeat<T> : IEnumerable<T>
    {
        readonly InfiniteEnumerator instance;

        public InfiniteRepeat(T value)
        {
            this.instance = new InfiniteEnumerator(value);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return instance;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return instance;
        }

        sealed class InfiniteEnumerator : IEnumerator<T>, IEnumerator
        {
            readonly T value;

            public InfiniteEnumerator(T value)
            {
                this.value = value;
            }

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

            public void Dispose()
            {

            }

            public bool MoveNext()
            {
                return true;
            }

            public void Reset()
            {
                
            }
        }

        public static IEnumerable<T> RepeatFinite(T value, long count)
        {
            for (long i = 0; i < count; i++)
            {
                yield return value;
            }
            yield break;
        }

        public static IEnumerable<T> PredicateRepeatUntil(T value, Func<bool> predicate)
        {
            do
            {
                yield return value;
            } while (predicate());
        }
    }
}
