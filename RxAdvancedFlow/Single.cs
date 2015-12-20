using ReactiveStreamsCS;
using RxAdvancedFlow.internals.completable;
using RxAdvancedFlow.internals.single;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Extension methods to create and transform ISingle streams.
    /// </summary>
    public static class Single
    {
        public static ISingle<T> Create<T>(Action<ISingleSubscriber<T>> onSubscribe)
        {
            return new SingleFromAction<T>(onSubscribe);
        }

        public static ISingle<R> Lift<T, R>(this ISingle<T> source, Func<ISingleSubscriber<R>, ISingleSubscriber<T>> onLift)
        {
            return new SingleLift<T, R>(source, onLift);
        }

        public static R To<T, R>(this ISingle<T> source, Func<ISingle<T>, R> converter)
        {
            return converter(source);
        }

        public static ISingle<R> Compose<T, R>(this ISingle<T> source, Func<ISingle<T>, ISingle<R>> composeFunction)
        {
            return To(source, composeFunction);
        }

        public static ICompletable ToCompletable<T>(this ISingle<T> source)
        {
            return Completable.Create(cs =>
            {
                SingleSubscriberToCompletableSubscriber<T> sscs = new SingleSubscriberToCompletableSubscriber<T>(cs);

                source.Subscribe(sscs);
            });
        }


    }
}
