using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.completable;
using RxAdvancedFlow.internals.publisher;
using System;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Extension methods to create and transform IPublisher streams.
    /// </summary>
    public static class Flowable
    {
        public static IPublisher<T> Create<T>(Action<ISubscriber<T>> onSubscribe)
        {
            return new PublisherFromAction<T>(onSubscribe);
        }

        public static IPublisher<R> Lift<T, R>(this IPublisher<T> source, Func<ISubscriber<R>, ISubscriber<T>> onLift)
        {
            return new PublisherLift<T, R>(source, onLift);
        }

        public static R To<T, R>(this IPublisher<T> source, Func<IPublisher<T>, R> converter)
        {
            return converter(source);
        }

        public static IPublisher<R> Compose<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> composer)
        {
            return To(source, composer);
        }

        public static ICompletable ToCompletable<T>(this IPublisher<T> source)
        {
            return Completable.Create(cs =>
            {
                SubscriberToCompletableSubscriber<T> stcs = new SubscriberToCompletableSubscriber<T>(cs);

                source.Subscribe(stcs);
            });
        }

        public static ICompletable AndThen<T>(this IPublisher<T> source, ICompletable other)
        {
            return source.ToCompletable().AndThen(other);
        }

        public static IPublisher<T> Just<T>(T value)
        {
            return new ScalarSource<T>(value);
        }


    }
}
