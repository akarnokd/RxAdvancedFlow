using ReactiveStreamsCS;
using RxAdvancedFlow.internals.queues;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals
{
    static class BackpressureHelper
    {
        /// <summary>
        /// Adds two long values and caps the sum at long.MaxValue.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static long AddCap(long a, long b)
        {
            long c = a + b;
            if (c < 0)
            {
                return long.MaxValue;
            }
            return c;
        }

        /// <summary>
        /// Multiplies two long values and caps the result at long.MaxValue.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static long MultiplyCap(long a, long b)
        {
            long c = a * b;

            if (((a | b) >> 31) != 0)
            {
                if (b != 0 && c / b != a)
                {
                    return long.MaxValue;
                }
            }

            return c;
        }

        /// <summary>
        /// Atomically adds the value to the target field and caps the sum
        /// at long.MaxValue.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static long Add(ref long field, long value)
        {
            for (;;)
            {
                long a = Volatile.Read(ref field);

                long u = AddCap(a, value);

                if (Interlocked.CompareExchange(ref field, u, a) == a)
                {
                    return a;
                }
            }
        }

        static readonly int STATE_NONE = 0;
        static readonly int STATE_HAS_REQUEST_NO_VALUE = 1;
        static readonly int STATE_NO_REQUEST_HAS_VALUE = 2;
        static readonly int STATE_HAS_REQUEST_HAS_VALUE = 3;

        /// <summary>
        /// In a single-value emitting backpressure case, this checks if the
        /// value can be emitted immediately or has to be deferred until
        /// a request comes along.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="state">The field holding the current state that will be atomically updated.</param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns>True if the value can be directly emitted.</returns>
        public static bool SetValue<T>(ref int state, ref T field, T value)
        {
            for (;;)
            {
                int s = Volatile.Read(ref state);
                if (s == STATE_HAS_REQUEST_HAS_VALUE || s == STATE_NO_REQUEST_HAS_VALUE)
                {
                    return false;
                }
                if (s == STATE_HAS_REQUEST_NO_VALUE)
                {
                    return Interlocked.CompareExchange(ref state, STATE_HAS_REQUEST_HAS_VALUE, s) == s;
                }
                field = value;
                if (Interlocked.CompareExchange(ref state, STATE_NO_REQUEST_HAS_VALUE, STATE_NONE) == STATE_NONE)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// In a single-value emitting backpressure case, this checks if there is a
        /// value already available or updates the state indicating there is
        /// a request for a value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="state"></param>
        /// <param name="value"></param>
        /// <returns>True if the contents of the value can be read and emitted</returns>
        public static bool SetRequest<T>(ref int state, ref T value)
        {
            for (;;)
            {
                int s = Volatile.Read(ref state);
                if (s == STATE_HAS_REQUEST_NO_VALUE || s == STATE_HAS_REQUEST_HAS_VALUE)
                {
                    return false;
                }
                if (s == STATE_NO_REQUEST_HAS_VALUE)
                {
                    return Interlocked.CompareExchange(ref state, STATE_HAS_REQUEST_HAS_VALUE, s) == s;
                }
                if (Interlocked.CompareExchange(ref state, STATE_HAS_REQUEST_NO_VALUE, STATE_NONE) == STATE_NONE)
                {
                    return false;
                }
            }

        }

        /// <summary>
        /// Sets the target state field into a terminal state so neither
        /// SetRequest nor SetValue does anything.
        /// </summary>
        /// <param name="state"></param>
        public static void SetTerminated(ref int state)
        {
            Interlocked.Exchange(ref state, STATE_HAS_REQUEST_HAS_VALUE);
        }

        /// <summary>
        /// Checks if the target state field contains the terminal state.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static bool IsTerminated(ref int state)
        {
            return Volatile.Read(ref state) == STATE_HAS_REQUEST_HAS_VALUE;
        }

        /// <summary>
        /// Atomically sets the single ISubscriber on the target field and requests the accumulated
        /// request amount. 
        /// </summary>
        /// <param name="field"></param>
        /// <param name="requested"></param>
        /// <param name="s"></param>
        /// <returns>False if the target field contains the common Cancelled instance or is not null</returns>
        public static bool SingleSetSubscription(ref ISubscription field, ref long requested, ISubscription s)
        {
            ISubscription a = Volatile.Read(ref field);
            
            if (a == SubscriptionHelper.Cancelled)
            {
                s.Cancel();
                return false;
            }

            a = Interlocked.CompareExchange(ref field, s, a);

            if (a == SubscriptionHelper.Cancelled)
            {
                s.Cancel();
                return false;
            }

            if (a == null)
            {
                long r = Interlocked.Exchange(ref requested, 0);

                if (r != 0)
                {
                    s.Request(r);
                }

                return true;
            }

            s.Cancel();
            OnSubscribeHelper.ReportSubscriptionSet();

            return false;
        }

        public static void SingleRequest(ref ISubscription s, ref long requested, long n)
        {
            ISubscription a = Volatile.Read(ref s);
            if (a != null)
            {
                a.Request(n);
            } 
            else
            {
                Add(ref requested, n);

                a = Volatile.Read(ref s);

                if (a != null)
                {
                    long r = Interlocked.Exchange(ref requested, 0);

                    if (r != 0)
                    {
                        a.Request(n);
                    }
                }
            }
        }

        /// <summary>
        /// Creates the default missing backpressure exception indicator.
        /// </summary>
        /// <returns></returns>
        public static Exception MissingBackpressureException()
        {
            return new InvalidOperationException("Backpressure not honored");
        }

        static readonly long COMPLETED_MASK = long.MinValue;
        static readonly long REQUESTED_MASK = long.MaxValue;

        public static void PostComplete<T>(ref long requested, IQueue<T> queue, ISubscriber<T> actual, ref bool cancelled)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);
                if ((r & COMPLETED_MASK) != 0)
                {
                    return;
                }
                long u = r | COMPLETED_MASK;

                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    if ((r & REQUESTED_MASK) != 0)
                    {
                        PostCompleteDrain(ref requested, u, queue, actual, ref cancelled);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Increment a requested amount and account for a post-complete state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="requested"></param>
        /// <param name="n"></param>
        /// <param name="queue"></param>
        /// <param name="actual"></param>
        /// <param name="cancelled"></param>
        /// <returns>False if in the post-complete state.</returns>
        public static bool PostCompleteRequest<T>(ref long requested, long n, IQueue<T> queue, ISubscriber<T> actual, ref bool cancelled)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);

                long u = (r & REQUESTED_MASK) + n;
                if (u < 0L)
                {
                    u = long.MaxValue; 
                }
                u |= r & COMPLETED_MASK;
                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    if ((r & REQUESTED_MASK) == 0)
                    {
                        PostCompleteDrain(ref requested, u, queue, actual, ref cancelled);
                    }
                    return r >= 0L;
                }
            }
        }

        static void PostCompleteDrain<T>(ref long requested, long n, 
            IQueue<T> queue, ISubscriber<T> actual, ref bool cancelled)
        {
            long e = n & COMPLETED_MASK;
            for (;;)
            {
                while (n != e)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    T t;

                    bool empty = !queue.Poll(out t);

                    if (empty)
                    {
                        actual.OnComplete();
                        return;
                    }

                    actual.OnNext(t);

                    e++;
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }

                if (queue.IsEmpty())
                {
                    actual.OnComplete();
                    return;
                }

                n = Volatile.Read(ref requested);
                if (n == e)
                {
                    n = Interlocked.Add(ref requested, -(e & REQUESTED_MASK));
                    if ((n & REQUESTED_MASK) == 0)
                    {
                        break;
                    } 
                    e = n & COMPLETED_MASK;
                }
            }
        }

        public static void ScalarPostComplete<T>(ref long requested, T value, ISubscriber<T> actual)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);

                long u = r | COMPLETED_MASK;
                
                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    if (r != 0L)
                    {
                        actual.OnNext(value);
                        actual.OnComplete();
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Atomically adds the request amount and returns false if in a post-complete state
        /// the value could and was emitted.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="requested"></param>
        /// <param name="n"></param>
        /// <param name="value"></param>
        /// <param name="actual"></param>
        /// <returns></returns>
        public static bool ScalarPostCompleteRequest<T>(ref long requested, long n, T value, ISubscriber<T> actual)
        {
            for (;;)
            {
                long r = Volatile.Read(ref requested);

                if (r == COMPLETED_MASK)
                {
                    if (Interlocked.CompareExchange(ref requested, r + 1, r) == r)
                    {
                        actual.OnNext(value);
                        actual.OnComplete();
                    }
                    return false;
                }
                
                if (r < 0)
                {
                    return false;
                }

                long u = AddCap(r, n);
                if (Interlocked.CompareExchange(ref requested, u, r) == r)
                {
                    return true;
                }
            }
        }
    }
}
