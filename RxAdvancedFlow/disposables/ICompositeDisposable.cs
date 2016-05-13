using System;

namespace RxAdvancedFlow.disposables
{
    public interface ICompositeDisposable : IDisposable
    {
        bool Add(IDisposable d);

        bool Remove(IDisposable d);

        bool Delete(IDisposable d);

        void Clear();

        bool IsEmpty();

        bool IsDisposed();
    }
}
