using System;

namespace RxAdvancedFlow.disposables
{
    public interface ISoloDisposable : IDisposable
    {
        IDisposable Get();

        bool Set(IDisposable d);

        bool IsDisposed();
    }
}
