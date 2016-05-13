using System;
using System.Threading;

namespace RxAdvancedFlow.internals.disposables
{
    /// <summary>
    /// A disposable class that calls a specified dispose action at most once.
    /// </summary>
    public sealed class ActionDisposable : IDisposable
    {
        readonly Action action;
        
        int once;

        public ActionDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                action();
            }
        }

        public bool IsDisposed()
        {
            return Volatile.Read(ref once) != 0;
        }
    }
}
