using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.disposables
{
    public sealed class MultipleAssignmentDisposable : ISoloDisposable
    {
        IDisposable d;

        public MultipleAssignmentDisposable()
        {

        }

        public MultipleAssignmentDisposable(IDisposable d)
        {
            this.d = d;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public IDisposable Get()
        {
            IDisposable a = Volatile.Read(ref d);
            if (a == DisposableHelper.Disposed)
            {
                return EmptyDisposable.Instance;
            }
            return a;
        }

        public bool IsDisposed()
        {
            return DisposableHelper.IsTerminated(ref d);
        }

        public bool Set(IDisposable d)
        {
            return DisposableHelper.Replace(ref this.d, d);
        }
    }
}
