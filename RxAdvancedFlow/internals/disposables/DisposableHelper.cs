using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.disposables
{
    /// <summary>
    /// Utility methods to work with IDisposable references by using terminal atomics.
    /// </summary>
    static class DisposableHelper
    {
        /// <summary>
        /// The single instance of an IDisposable which indicates a disposed state.
        /// </summary>
        public static readonly IDisposable Disposed = new DefaultDisposed();    

        /// <summary>
        /// Atomically swaps in the common disposed instance and disposes any previous
        /// non-null content.
        /// </summary>
        /// <param name="d">The target field to swap with the common disposed instance</param>
        public static bool Terminate(ref IDisposable d)
        {
            IDisposable a = Volatile.Read(ref d);
            if (a != Disposed)
            {
                a = Interlocked.Exchange(ref d, Disposed);
                if (a != Disposed)
                {
                    a?.Dispose();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if the given disposable reference holds the common disposed instance.
        /// </summary>
        /// <param name="d">The target field to check</param>
        /// <returns>True if the field contains the common disposed instance.</returns>
        public static bool IsTerminated(ref IDisposable d)
        {
            return Volatile.Read(ref d) == Disposed;
        }

        /// <summary>
        /// Atomically sets the reference contents with the new disposable value and
        /// disposes the old one, or disposes the value if the target contains the common
        /// disposed instance.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool Set(ref IDisposable d, IDisposable value)
        {
            for (;;)
            {
                IDisposable a = Volatile.Read(ref d);
                if (a == Disposed)
                {
                    value?.Dispose();
                    return false;
                }

                if (Interlocked.CompareExchange(ref d, value, a) == a)
                {
                    a?.Dispose();
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically replaces the contents of the reference field with the disposable
        /// value but does not dispose the old value, or disposes the value if the 
        /// target contains the common disposed instance.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool Replace(ref IDisposable d, IDisposable value)
        {
            for (;;)
            {
                IDisposable a = Volatile.Read(ref d);
                if (a == Disposed)
                {
                    value?.Dispose();
                    return false;
                }

                if (Interlocked.CompareExchange(ref d, value, a) == a)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically sets the content of the reference and returns true or
        /// returns false if the target reference is not null.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool SetOnce(ref IDisposable d, IDisposable value)
        {
            for (;;)
            {
                IDisposable a = Volatile.Read(ref d);
                if (a == Disposed)
                {
                    value?.Dispose();
                    return true;
                }

                if (a != null)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref d, value, null) == null)
                {
                    return true;
                }
            }
        }
    }

    sealed class DefaultDisposed : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
