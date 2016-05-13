using System;
using System.Threading;

namespace RxAdvancedFlow.internals
{
    /// <summary>
    /// Utility methods to handle an array of some subscribers atomically.
    /// </summary>
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

        public static bool Add<T>(object guard, ref T[] array, T newValue, T[] terminated) where T : class
        {
            T[] a = Volatile.Read(ref array);

            if (a == terminated)
            {
                return false;
            }

            lock (guard)
            {
                a = Volatile.Read(ref array);

                if (a == terminated)
                {
                    return false;
                }

                int n = a.Length;

                T[] b = new T[n + 1];
                Array.Copy(a, 0, b, 0, n);
                b[n] = newValue;

                Volatile.Write(ref array, b);
                return true;
            }
        }

        public static bool Remove<T>(object guard, ref T[] array, T newValue, T[] terminated, T[] empty) where T : class
        {
            T[] a = Volatile.Read(ref array);

            if (a == terminated || a == empty)
            {
                return false;
            }

            lock (guard)
            {
                a = Volatile.Read(ref array);

                if (a == terminated || a == empty)
                {
                    return false;
                }

                int n = a.Length;
                int j = -1;

                for (int i = 0; i < n; i++)
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
                }
                else
                {
                    b = new T[n - 1];
                    Array.Copy(a, 0, b, 0, j);
                    Array.Copy(a, j + 1, b, j, n - j - 1);
                }
                Volatile.Write(ref array, b);

                return true;
            }
        }

        public static T[] Terminate<T>(ref T[] array, T[] terminated) where T : class
        {
            T[] a = Volatile.Read(ref array);
            if (a != terminated)
            {
                a = Interlocked.Exchange(ref array, terminated);
            }

            return a;
        }

        public static T[] Terminate<T>(object guard, ref T[] array, T[] terminated) where T : class
        {
            T[] a = Volatile.Read(ref array);
            if (a != terminated)
            {
                lock (guard)
                {
                    a = Interlocked.Exchange(ref array, terminated);
                }
            }

            return a;
        }
    }
}
