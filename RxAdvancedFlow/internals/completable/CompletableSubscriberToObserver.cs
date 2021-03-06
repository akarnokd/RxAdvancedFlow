﻿using RxAdvancedFlow.internals.disposables;
using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableSubscriberToObserver<T> : IObserver<T>, IDisposable
    {
        readonly ICompletableSubscriber cs;

        IDisposable d;

        public CompletableSubscriberToObserver(ICompletableSubscriber s)
        {
            cs = s;
        }

        public void Dispose()
        {
            DisposableHelper.Terminate(ref d);
        }

        public void Set(IDisposable value)
        {
            DisposableHelper.Set(ref d, value);
        }

        public void OnCompleted()
        {
            cs.OnComplete();
        }

        public void OnError(Exception error)
        {
            cs.OnError(error);
        }

        public void OnNext(T value)
        {
            // ignored
        }

        
    }
}
