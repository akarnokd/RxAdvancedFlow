﻿using System;

namespace RxAdvancedFlow.internals.completable
{
    sealed class CompletableLift : ICompletable
    {
        readonly ICompletable source;
        readonly Func<ICompletableSubscriber, ICompletableSubscriber> onLift;

        public CompletableLift(ICompletable source, Func<ICompletableSubscriber, ICompletableSubscriber> onLift)
        {
            this.source = source;
            this.onLift = onLift;
        }

        public void Subscribe(ICompletableSubscriber s)
        {
            ICompletableSubscriber parent = onLift(s);

            source.Subscribe(parent);
        }
    }
}
