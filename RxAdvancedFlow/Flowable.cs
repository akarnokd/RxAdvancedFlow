using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.completable;
using RxAdvancedFlow.internals.publisher;
using RxAdvancedFlow.internals.subscriptions;
using RxAdvancedFlow.subscriptions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Extension methods to create and transform IPublisher streams.
    /// </summary>
    public static class Flowable
    {
        static readonly int DefaultBufferSize = GetSettingsBufferSize();

        static int GetSettingsBufferSize()
        {
            string s = Environment.GetEnvironmentVariable("rxaf.buffer-size");

            int i;

            if (int.TryParse(s, out i))
            {
                return Math.Max(1, i);
            }

            return 128;
        }

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

        public static IPublisher<T> AmbArray<T>(params IPublisher<T>[] sources)
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

        public static IPublisher<T> Amb<T>(this IPublisher<T>[] sources)
        {
            return AmbArray(sources);
        }

        static T[] ToArray<T>(IEnumerable<T> ie, out int n)
        {
            T[] a = new T[8];
            int c = 0;

            foreach (T t in ie)
            {
                if (c == a.Length)
                {
                    T[] b = new T[c + (c >> 2)];
                    Array.Copy(a, 0, b, 0, c);
                    a = b;
                }
                a[c++] = t;
            }

            n = c;
            return a;
        }

        public static IPublisher<T> Amb<T>(this IEnumerable<IPublisher<T>> sources)
        {
            return Create<T>(s =>
            {
                int n;
                IPublisher<T>[] a = ToArray(sources, out n);

                if (n == 0)
                {
                    EmptySubscription.Complete(s);
                    return;
                }
                else
                if (n == 1)
                {
                    a[0].Subscribe(s);
                    return;
                }

                PublisherAmbCoordinator<T> ambc = new PublisherAmbCoordinator<T>(s);

                ambc.Subscribe(a, n);
            });
        }

        public static int BufferSize()
        {
            return DefaultBufferSize;
        }

        public static IPublisher<R> CombineLatest<T, R>(this IPublisher<T>[] sources, Func<T[], R> combiner)
        {
            return CombineLatest(sources, combiner, BufferSize());
        }

        public static IPublisher<R> CombineLatest<T, R>(this IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner)
        {
            return CombineLatest(sources, combiner, BufferSize());
        }

        public static IPublisher<R> CombineLatest<T, R>(this IPublisher<T>[] sources, Func<T[], R> combiner, int bufferSize)
        {
            return Create<R>(s =>
            {
                PublisherCombineCoordinator<T, R> pcc = new PublisherCombineCoordinator<T, R>(s, combiner, bufferSize);

                pcc.Subscribe(sources, sources.Length);
            });
        }

        public static IPublisher<R> CombineLatest<T, R>(this IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner, int bufferSize)
        {
            return Create<R>(s =>
            {
                int n;
                IPublisher<T>[] a = ToArray(sources, out n);

                if (n == 0)
                {
                    EmptySubscription.Complete(s);
                    return;
                }
                if (n == 1)
                {
                    a[0].Map(v => combiner(new T[] { v })).Subscribe(s);
                    return;
                }

                PublisherCombineCoordinator<T, R> pcc = new PublisherCombineCoordinator<T, R>(s, combiner, bufferSize);

                pcc.Subscribe(a, n);
            });
        }

        public static IPublisher<R> CombineLatest<T1, T2, R>(
            IPublisher<T1> s1, IPublisher<T2> s2, 
            Func<T1, T2, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3,
            Func<T1, T2, T3, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2,
                (IPublisher<object>)s3
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            Func<T1, T2, T3, T4, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2,
                (IPublisher<object>)s3, (IPublisher<object>)s4
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5,
            Func<T1, T2, T3, T4, T5, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2,
                (IPublisher<object>)s3, (IPublisher<object>)s4,
                (IPublisher<object>)s5
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            Func<T1, T2, T3, T4, T5, T6, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2,
                (IPublisher<object>)s3, (IPublisher<object>)s4,
                (IPublisher<object>)s5, (IPublisher<object>)s6
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, T7, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            IPublisher<T7> s7,
            Func<T1, T2, T3, T4, T5, T6, T7, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2,
                (IPublisher<object>)s3, (IPublisher<object>)s4,
                (IPublisher<object>)s5, (IPublisher<object>)s6,
                (IPublisher<object>)s7
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, T7, T8, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            IPublisher<T7> s7, IPublisher<T8> s8,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2,
                (IPublisher<object>)s3, (IPublisher<object>)s4,
                (IPublisher<object>)s5, (IPublisher<object>)s6,
                (IPublisher<object>)s7, (IPublisher<object>)s8
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            IPublisher<T7> s7, IPublisher<T8> s8,
            IPublisher<T9> s9,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> combiner)
        {
            return CombineLatest(new IPublisher<object>[] {
                (IPublisher<object>)s1, (IPublisher<object>)s2,
                (IPublisher<object>)s3, (IPublisher<object>)s4,
                (IPublisher<object>)s5, (IPublisher<object>)s6,
                (IPublisher<object>)s7, (IPublisher<object>)s8,
                (IPublisher<object>)s9
            }, LambdaHelper.ToFuncN(combiner));
        }

        public static IPublisher<T> Concat<T>(this IPublisher<T>[] sources)
        {
            return Concat((IEnumerable<IPublisher<T>>)sources);
        }

        public static IPublisher<T> ConcatArray<T>(params IPublisher<T>[] sources)
        {
            return Concat((IEnumerable<IPublisher<T>>)sources);
        }

        public static IPublisher<T> Concat<T>(this IEnumerable<IPublisher<T>> sources)
        {
            return Create<T>(s =>
            {
                PublisherConcatSubscriber<T> pcs = new PublisherConcatSubscriber<T>(s, sources.GetEnumerator());

                s.OnSubscribe(pcs);

                pcs.OnComplete();
            });
        }

        public static IPublisher<T> Concat<T>(this IPublisher<IPublisher<T>> sources, int prefetch = 2)
        {
            return ConcatMap(sources, v => v, prefetch);
        }

        public static IPublisher<T> Defer<T>(Func<IPublisher<T>> publisherFactory)
        {
            return Create<T>(s =>
            {
                IPublisher<T> p;

                try
                {
                    p = publisherFactory();
                }
                catch (Exception e)
                {
                    EmptySubscription.Error(s, e);
                    return;
                }

                if (EmptySubscription.NullCheck(s, p))
                {
                    p.Subscribe(s);
                }
            });
        }



        public static IPublisher<R> Map<T, R>(this IPublisher<T> source, Func<T, R> mapper)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherMapSubscriber<T, R>(s, mapper));
            });
        }

        public static IPublisher<R> ConcatMap<T, R>(this IPublisher<T> source, Func<T, IPublisher<R>> mapper, int prefetch = 2)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherConcatMapSubscriber<T, R>(s, mapper, prefetch));
            });
        }


        public static IPublisher<T> FromArray<T>(params T[] array)
        {
            int len = array.Length;
            if (len == 0)
            {
                return Empty<T>();
            }
            else
            if (len == 1)
            {
                return Just<T>(array[0]);
            }
            return Create<T>(s =>
            {
                s.OnSubscribe(new PublisherFromArray<T>(s, array));
            });
        }

        public static IPublisher<T> FromEnumerable<T>(IEnumerable<T> source)
        {
            return Create<T>(s =>
            {
                IEnumerator<T> et = source.GetEnumerator();

                bool b;

                try
                {
                    b = et.MoveNext();
                }
                catch (Exception ex)
                {
                    EmptySubscription.Error(s, ex);
                    return;
                }

                if (!b)
                {
                    EmptySubscription.Complete(s);
                    return;
                }

                s.OnSubscribe(new PublisherFromEnumerable<T>(s, et));
            });
        }

        public static IPublisher<T> FromTask<T>(Task<T> task)
        {
            return Create<T>(s =>
            {
                ScalarDelayedSubscription<T> sds = new ScalarDelayedSubscription<T>(s);

                s.OnSubscribe(sds);

                task.ContinueWith(t =>
                {
                    Exception e = t.Exception;
                    if (e != null)
                    {
                        s.OnError(e);
                    }
                    else
                    {
                        sds.Set(t.Result);
                    }
                });
            });
        }

        public static IPublisher<T> FromFunction<T>(Func<T> function)
        {
            return Create<T>(s =>
            {
                ScalarDelayedSubscription<T> sds = new ScalarDelayedSubscription<T>(s);

                s.OnSubscribe(sds);

                T t;

                try
                {
                    t = function();
                }
                catch (Exception ex)
                {
                    s.OnError(ex);
                    return;
                }

                sds.Set(t);
            });
        }

        public static IPublisher<long> Interval(TimeSpan period)
        {
            return Interval(period, period, DefaultScheduler.Instance);
        }

        public static IPublisher<long> Interval(TimeSpan period, IScheduler scheduler)
        {
            return Interval(period, period, scheduler);
        }

        public static IPublisher<long> Interval(TimeSpan initialDelay, TimeSpan period)
        {
            return Interval(initialDelay, period, DefaultScheduler.Instance);
        }

        public static IPublisher<long> Interval(TimeSpan initialDelay, TimeSpan period, IScheduler scheduler)
        {
            return Create<long>(s =>
            {
                PublisherInterval pi = new PublisherInterval(s);

                s.OnSubscribe(pi);

                pi.SetTimer(scheduler.SchedulePeriodicallyDirect(pi.Run, initialDelay, period));
            });
        }

        public static IPublisher<T> Merge<T>(int maxConcurrency = int.MaxValue, params IPublisher<T>[] sources)
        {
            return FromArray(sources).FlatMap(v => v, false, maxConcurrency);
        }

        public static IPublisher<T> MergeDelayError<T>(int maxConcurrency = int.MaxValue, params IPublisher<T>[] sources)
        {
            return FromArray(sources).FlatMap(v => v, true, maxConcurrency);
        }

        public static IPublisher<T> Merge<T>(this IEnumerable<IPublisher<T>> sources, int maxConcurrency = int.MaxValue)
        {
            return FromEnumerable(sources).FlatMap(v => v, false, maxConcurrency);
        }

        public static IPublisher<T> MergeDelayError<T>(this IEnumerable<IPublisher<T>> sources, int maxConcurrency = int.MaxValue)
        {
            return FromEnumerable(sources).FlatMap(v => v, true, maxConcurrency);
        }

        public static IPublisher<R> FlatMap<T, R>(this IPublisher<T> source, Func<T, IPublisher<R>> mapper, bool delayError = false, int maxConcurrency = int.MaxValue)
        {
            return FlatMap(source, mapper, delayError, maxConcurrency, BufferSize());
        }

        public static IPublisher<R> FlatMap<T, R>(this IPublisher<T> source, Func<T, IPublisher<R>> mapper, bool delayError = false, int maxConcurrency = int.MaxValue, int bufferSize)
        {
            return Create<R>(s =>
            {
                PublisherMerge<T, R> parent = new PublisherMerge<T, R>(s, maxConcurrency, delayError, bufferSize, mapper);

                s.OnSubscribe(parent);

                source.Subscribe(parent);
            });
        }

    }
}
