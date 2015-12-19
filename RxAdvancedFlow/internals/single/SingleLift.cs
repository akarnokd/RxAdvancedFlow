using ReactiveStreamsCS;
using RxAdvancedFlow.internals.disposables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class SingleLift<T, R> : ISingle<R>
    {
        readonly ISingle<T> source;

        readonly Func<ISingleSubscriber<R>, ISingleSubscriber<T>> onLift;

        public SingleLift(ISingle<T> source, Func<ISingleSubscriber<R>, ISingleSubscriber<T>> onLift)
        {
            this.source = source;
            this.onLift = onLift;
        }

        public void Subscribe(ISingleSubscriber<R> s)
        {
            ISingleSubscriber<T> sr;

            try
            {
                sr = onLift(s);
            }
            catch (Exception ex)
            {
                EmptyDisposable.Error(s, ex);
                return;
            }
            source.Subscribe(sr);
        }
    }
}
