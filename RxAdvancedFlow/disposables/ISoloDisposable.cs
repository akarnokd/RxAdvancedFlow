using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.disposables
{
    public interface ISoloDisposable : IDisposable
    {
        IDisposable Get();

        bool Set(IDisposable d);

        bool IsDisposed();
    }
}
