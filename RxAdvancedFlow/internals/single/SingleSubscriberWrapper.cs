﻿using RxAdvancedFlow.disposables;
using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class SingleSubscriberWrapper<T> : ISingleSubscriber<T>
    {
        readonly ISingleSubscriber<T> actual;

        readonly ISoloDisposable disposable;

        public SingleSubscriberWrapper(ISingleSubscriber<T> actual, ISoloDisposable disposable)
        {
            this.actual = actual;
            this.disposable = disposable;
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnSubscribe(IDisposable d)
        {
            disposable.Set(d);
        }

        public void OnSuccess(T t)
        {
            actual.OnSuccess(t);
        }
    }
}
