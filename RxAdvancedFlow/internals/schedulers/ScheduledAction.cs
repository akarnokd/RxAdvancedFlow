using RxAdvancedFlow.disposables;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.schedulers
{
    sealed class ScheduledAction : IDisposable
    {
        readonly Action action;

        IDisposable parent;

        IDisposable cancel;

        static readonly IDisposable Finished = new FinishedDisposable();

        sealed class FinishedDisposable : IDisposable
        {
            public void Dispose()
            {
                // deliberately no op
            }
        }

        public ScheduledAction(Action action, ICompositeDisposable parent = null)
        {
            this.action = action;
            this.parent = parent;
        }

        public void Run()
        {
            if (Volatile.Read(ref cancel) != DisposableHelper.Instance)
            {
                try
                {
                    action();
                }
                finally
                {
                    Finish();
                }
            }
        }

        void Finish()
        {
            IDisposable c = Volatile.Read(ref parent);
            if (c != DisposableHelper.Instance)
            {
                c = Interlocked.CompareExchange(ref parent, Finished, c);

                if (c != DisposableHelper.Instance)
                {
                    (c as ICompositeDisposable)?.Delete(this);
                }
            }

            c = Volatile.Read(ref cancel);
            if (c != DisposableHelper.Instance)
            {
                Interlocked.CompareExchange(ref cancel, Finished, c);
            }
        }

        public void SetCancel(IDisposable c)
        {
            IDisposable a = Volatile.Read(ref cancel);
            if (a == null)
            {
                a = Interlocked.CompareExchange(ref cancel, c, null);

                if (a == DisposableHelper.Instance)
                {
                    c.Dispose();
                }
            }
        }

        public void Dispose()
        {
            IDisposable a = Volatile.Read(ref cancel);

            if (a != Finished && a != DisposableHelper.Instance)
            {
                a = Interlocked.CompareExchange(ref cancel, DisposableHelper.Instance, a);

                if (a != null)
                {
                    a.Dispose();
                }
            }

            a = Volatile.Read(ref parent);

            if (a != Finished && a != DisposableHelper.Instance)
            {
                a = Interlocked.CompareExchange(ref parent, DisposableHelper.Instance, a);

                if (a != Finished && a != DisposableHelper.Instance)
                {
                    (a as ICompositeDisposable)?.Delete(this);
                }
            }
        }
    }
}
