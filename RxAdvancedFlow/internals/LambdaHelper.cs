using System;

namespace RxAdvancedFlow.internals
{
    /// <summary>
    /// Lambda conversion helper methods.
    /// </summary>
    static class LambdaHelper
    {
        public static Func<T[], R> ToFuncN<T, R>(Func<T, T, R> func)
        {
            return arr => func(arr[0], arr[1]);
        }

        public static Func<object[], R> ToFuncN<T1, T2, T3, R>(Func<T1, T2, T3, R> func)
        {
            return arr => func((T1)arr[0], (T2)arr[1], (T3)arr[2]);
        }

        public static Func<object[], R> ToFuncN<T1, T2, T3, T4, R>(
            Func<T1, T2, T3, T4, R> func)
        {
            return arr => func((T1)arr[0], (T2)arr[1], (T3)arr[2], (T4)arr[3]);
        }

        public static Func<object[], R> ToFuncN<T1, T2, T3, T4, T5, R>(
            Func<T1, T2, T3, T4, T5, R> func)
        {
            return arr => func((T1)arr[0], (T2)arr[1], (T3)arr[2], (T4)arr[3], (T5)arr[4]);
        }

        public static Func<object[], R> ToFuncN<T1, T2, T3, T4, T5, T6, R>(
            Func<T1, T2, T3, T4, T5, T6, R> func)
        {
            return arr => func((T1)arr[0], (T2)arr[1], (T3)arr[2], (T4)arr[3], (T5)arr[4], (T6)arr[5]);
        }

        public static Func<object[], R> ToFuncN<T1, T2, T3, T4, T5, T6, T7, R>(
            Func<T1, T2, T3, T4, T5, T6, T7, R> func)
        {
            return arr => func((T1)arr[0], (T2)arr[1], (T3)arr[2], (T4)arr[3], (T5)arr[4], (T6)arr[5], (T7)arr[6]);
        }

        public static Func<object[], R> ToFuncN<T1, T2, T3, T4, T5, T6, T7, T8, R>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, R> func)
        {
            return arr => func((T1)arr[0], (T2)arr[1], (T3)arr[2], (T4)arr[3], (T5)arr[4], (T6)arr[5], (T7)arr[6], (T8)arr[7]);
        }

        public static Func<object[], R> ToFuncN<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> func)
        {
            return arr => func((T1)arr[0], (T2)arr[1], (T3)arr[2], (T4)arr[3], (T5)arr[4], (T6)arr[5], (T7)arr[6], (T8)arr[7], (T9)arr[8]);
        }
    }
}
