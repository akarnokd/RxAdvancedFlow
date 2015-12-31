using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherSingle<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        int count;

        T value;

        public PublisherSingle(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void OnComplete()
        {
            if (count == 0)
            {
                actual.OnError(new IndexOutOfRangeException("Source is empty"));
            }
            else
            if (count == 1)
            {
                actual.OnNext(value);
                actual.OnComplete();
            }
        }

        public void OnError(Exception e)
        {
            if (count <= 1)
            {
                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnNext(T t)
        {
            int c = count;
            if (c == 0)
            {
                value = t;
                count = 1;
            } else
            if (c == 1)
            {
                count = 2;
                s.Cancel();

                actual.OnError(new IndexOutOfRangeException("Source contains more than one element"));
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                s.Request(long.MaxValue);
            }
        }

        public void Cancel()
        {
            s.Cancel();
        }
    }

    sealed class PublisherSingleDefault<T> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        int count;

        T value;

        public PublisherSingleDefault(ISubscriber<T> actual, T defaultValue)
        {
            this.actual = actual;
            this.value = defaultValue;
        }

        public void OnComplete()
        {
            if (count <= 1)
            {
                actual.OnNext(value);
                actual.OnComplete();
            }
        }

        public void OnError(Exception e)
        {
            if (count <= 1)
            {
                actual.OnError(e);
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }

        public void OnNext(T t)
        {
            int c = count;
            if (c == 0)
            {
                value = t;
                count = 1;
            }
            else
            if (c == 1)
            {
                count = 2;
                s.Cancel();

                actual.OnError(new IndexOutOfRangeException("Source contains more than one element"));
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                s.Request(long.MaxValue);
            }
        }

        public void Cancel()
        {
            s.Cancel();
        }
    }
}
