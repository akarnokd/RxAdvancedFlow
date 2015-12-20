using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.processors
{
    public sealed class SingleProcessor<T> : ISingleProcessor<T, T>
    {
        public void OnError(Exception e)
        {
            throw new NotImplementedException();
        }

        public void OnSubscribe(IDisposable d)
        {
            throw new NotImplementedException();
        }

        public void OnSuccess(T t)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(ISingleSubscriber<T> s)
        {
            throw new NotImplementedException();
        }
    }
}
