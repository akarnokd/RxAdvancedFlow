using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals
{
    static class ProcessorHelper
    {
        public static bool Add<T>(ref T[] array, T newValue, T[] terminated) where T : class
        {
            for (;;)
            {
                T[] a = Volatile.Read(ref array);

                if (a == terminated)
                {
                    return false;
                }

                int n = a.Length;

                T[] b = new T[n + 1];
                Array.Copy(a, 0, b, 0, n);
                b[n] = newValue;

                if (Interlocked.CompareExchange(ref array, b, a) == a)
                {
                    return true;
                }
            }
        }

        public static bool Remove<T>(ref T[] array, T newValue, T[] terminated, T[] empty) where T : class
        {
            for (;;)
            {
                T[] a = Volatile.Read(ref array);

                if (a == terminated || a == empty)
                {
                    return false;
                }

                int n = a.Length;
                int j = -1;

                for(int i = 0; i < n; i++)
                {
                    if (a[i] == newValue)
                    {
                        j = i;
                        break;
                    }
                }

                if (j < 0)
                {
                    return false;
                }

                T[] b;
                if (n == 1)
                {
                    b = empty;
                } else
                {
                    b = new T[n - 1];
                    Array.Copy(a, 0, b, 0, j);
                    Array.Copy(a, j + 1, b, j, n - j - 1);
                }
                if (Interlocked.CompareExchange(ref array, b, a) == a)
                {
                    return true;
                }
            }
        }
    }
}
