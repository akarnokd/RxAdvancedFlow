using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.single
{
    sealed class MapSingleSubscriber<T, R> : ISingleSubscriber<T>
    {
        readonly ISingleSubscriber<R> actual;

        readonly Func<T, R> mapper;

        public MapSingleSubscriber(ISingleSubscriber<R> actual, Func<T, R> mapper)
        {
            this.actual = actual;
            this.mapper = mapper;
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            actual.OnSubscribe(d);
        }

        public void OnSuccess(T t)
        {
            R r;

            try
            {
                r = mapper(t);
            }
            catch (Exception e)
            {
                actual.OnError(e);
                return;
            }
            actual.OnSuccess(r);
        }
    }
}
