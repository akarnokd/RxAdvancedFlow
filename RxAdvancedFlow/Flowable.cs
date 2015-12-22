using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.completable;
using RxAdvancedFlow.internals.publisher;
using RxAdvancedFlow.internals.subscriptions;
using System;
using System.Collections.Generic;

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

        public static IPublisher<T> Empty<T>()
        {
            return Create<T>(s => EmptySubscription.Complete(s));
        }

        public static IPublisher<T> Never<T>()
        {
            return Create<T>(s => s.OnSubscribe(EmptySubscription.Instance));
        }

        public static IPublisher<T> Throw<T>(Exception error)
        {
            return Throw<T>(() => error);
        }

        public static IPublisher<T> Throw<T>(Func<Exception> errorSupplier)
        {
            return Create<T>(s =>
            {
                s.OnSubscribe(EmptySubscription.Instance);

                Exception e;

                try
                {
                    e = errorSupplier();
                }
                catch (Exception ex)
                {
                    e = ex;
                }

                s.OnError(e);
            });
        }

        public static IPublisher<T> Amb<T>(this IPublisher<T>[] sources)
        {
            int n = sources.Length;
            if (n == 0)
            {
                return Empty<T>();
            }
            if (n == 1)
            {
                return sources[0];
            }

            return Create<T>(s =>
            {
                PublisherAmbCoordinator<T> ambc = new PublisherAmbCoordinator<T>(s);

                ambc.Subscribe(sources, sources.Length);
            });
        }

        public static IPublisher<T> Amb<T>(this IEnumerable<IPublisher<T>> sources)
        {
            return Create<T>(s =>
            {
                IPublisher<T>[] a = new IPublisher<T>[8];
                int n = 0;

                foreach (IPublisher<T> p in sources)
                {
                    if (n == a.Length)
                    {
                        IPublisher<T>[] b = new IPublisher<T>[n + (n >> 2)];
                        Array.Copy(a, 0, b, 0, n);
                        a = b;
                    }
                    a[n] = p;
                }

                PublisherAmbCoordinator<T> ambc = new PublisherAmbCoordinator<T>(s);

                ambc.Subscribe(a, n);
            });
        }
    }
}
