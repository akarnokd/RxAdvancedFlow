using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals
{
    static class ExceptionHelper
    {
        public static readonly Exception TerminalException = new Exception();

        /// <summary>
        /// Atomically sets or aggregates the exceptions in the field
        /// and returns true if successful or false if the target
        /// field has been terminated (so that the exception can
        /// be sent to somewhere else).
        /// </summary>
        /// <param name="field"></param>
        /// <param name="newException"></param>
        /// <returns></returns>
        public static bool Add(ref Exception field, Exception newException)
        {
            for (;;)
            {
                Exception e = Volatile.Read(ref field);

                if (e == TerminalException)
                {
                    return false;
                }

                Exception f;

                if (e == null)
                {
                    f = newException;
                }
                else
                if (e is AggregateException)
                {
                    AggregateException a = e as AggregateException;

                    f = new AggregateException(ConcatWith(a.InnerExceptions, newException));
                }
                else
                {
                    f = new AggregateException(e, newException);
                }

                if (Interlocked.CompareExchange(ref field, f, e) == e)
                {
                    return true;
                }
            }
        }

        static IEnumerable<T> ConcatWith<T>(IEnumerable<T> first, T then)
        {
            foreach (T t in first)
            {
                yield return t;
            }
            yield return then;
            yield break;
        }

        public static bool Terminate(ref Exception field, out Exception last)
        {
            Exception e = Volatile.Read(ref field);

            if (e != TerminalException)
            {
                e = Interlocked.Exchange(ref field, TerminalException);
                if (e != TerminalException)
                {
                    last = e;
                    return true;
                }
            }
            last = null;
            return false;
        }
    }
}
