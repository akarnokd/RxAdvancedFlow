using System;

namespace RxAdvancedFlow.internals.disposables
{
    /// <summary>
    /// A disposable class that calls an action when it is disposed, but
    /// doesn't ensure idempotence to Dispose() call by itself.
    /// </summary>
    public sealed class ActionWeakDisposable : IDisposable
    {
        readonly Action action;

        public ActionWeakDisposable(Action action)
        {
            this.action = action;
        }

        public void Dispose()
        {
            action();
        }
    }
}
