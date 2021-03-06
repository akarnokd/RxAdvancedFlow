﻿using System;

namespace RxAdvancedFlow.internals.single
{
    sealed class SingleToObservable<T> : IObservable<T>
    {
        readonly ISingle<T> source;

        public SingleToObservable(ISingle<T> source)
        {
            this.source = source;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ObserverToSingleSubscriber<T> oss = new ObserverToSingleSubscriber<T>(observer);

            source.Subscribe(oss);

            return oss;
        }
            
    }
}
