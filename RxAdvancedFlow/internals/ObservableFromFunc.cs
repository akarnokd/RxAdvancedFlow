using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals
{
    sealed class ObservableFromFunc<T> : IObservable<T>
    {
        readonly Func<IObserver<T>, IDisposable> onSubscribe;

        public ObservableFromFunc(Func<IObserver<T>, IDisposable> onSubscribe)
        {
            this.onSubscribe = onSubscribe;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            try
            {
                return onSubscribe(observer);
            }
            catch (Exception ex)
            {
                RxAdvancedFlowPlugins.OnError(ex);
                return EmptyDisposable.Instance;
            }
        }
    }
}
