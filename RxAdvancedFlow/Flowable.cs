using ReactiveStreamsCS;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.completable;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.publisher;
using RxAdvancedFlow.internals.subscribers;
using RxAdvancedFlow.internals.subscriptions;
using RxAdvancedFlow.processors;
using RxAdvancedFlow.subscribers;
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
            return PublisherEmpty<T>.Instance;
        }

        public static IPublisher<T> Never<T>()
        {
            return PublisherNever<T>.Instance;
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
                PublisherAmb<T> ambc = new PublisherAmb<T>(s, n);

                ambc.Subscribe(sources, n);
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

                PublisherAmb<T> ambc = new PublisherAmb<T>(s, n);

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
                PublisherCombineLatest<T, R> pcc = new PublisherCombineLatest<T, R>(s, combiner, bufferSize);

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

                PublisherCombineLatest<T, R> pcc = new PublisherCombineLatest<T, R>(s, combiner, bufferSize);

                pcc.Subscribe(a, n);
            });
        }

        public static IPublisher<R> CombineLatest<T1, T2, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            Func<T1, T2, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3,
            Func<T1, T2, T3, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            Func<T1, T2, T3, T4, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5,
            Func<T1, T2, T3, T4, T5, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            Func<T1, T2, T3, T4, T5, T6, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, T7, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            IPublisher<T7> s7,
            Func<T1, T2, T3, T4, T5, T6, T7, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, T7, T8, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            IPublisher<T7> s7, IPublisher<T8> s8,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
        }

        public static IPublisher<R> CombineLatest<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T4> s4,
            IPublisher<T5> s5, IPublisher<T6> s6,
            IPublisher<T7> s7, IPublisher<T8> s8,
            IPublisher<T9> s9,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> combiner)
        {
            // TODO requires custom implementation of PublisherCombineLatest
            throw new NotImplementedException();
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
                PublisherConcatEnumerable<T> pcs = new PublisherConcatEnumerable<T>(s, sources.GetEnumerator());

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
                source.Subscribe(new PublisherMap<T, R>(s, mapper));
            });
        }

        public static IPublisher<R> ConcatMap<T, R>(this IPublisher<T> source, Func<T, IPublisher<R>> mapper, int prefetch = 2)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherConcatMap<T, R>(s, mapper, prefetch));
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

        public static IPublisher<T> Merge<T>(this IPublisher<T>[] sources, int maxConcurrency = int.MaxValue)
        {
            return FromArray(sources).FlatMap(v => v, false, maxConcurrency);
        }

        public static IPublisher<T> MergeDelayError<T>(this IPublisher<T>[] sources, int maxConcurrency = int.MaxValue)
        {
            return FromArray(sources).FlatMap(v => v, true, maxConcurrency);
        }

        public static IPublisher<T> Merge<T>(params IPublisher<T>[] sources)
        {
            return FromArray(sources).FlatMap(v => v, false);
        }

        public static IPublisher<T> Merge<T>(int maxConcurrency, params IPublisher<T>[] sources)
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

        public static IPublisher<T> Merge<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency = int.MaxValue)
        {
            return sources.FlatMap(v => v, false, maxConcurrency);
        }

        public static IPublisher<T> MergeDelayError<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency = int.MaxValue)
        {
            return sources.FlatMap(v => v, true, maxConcurrency);
        }

        public static IPublisher<R> FlatMap<T, R>(this IPublisher<T> source, Func<T, IPublisher<R>> mapper, bool delayError = false, int maxConcurrency = int.MaxValue)
        {
            return FlatMap(source, mapper, BufferSize(), delayError, maxConcurrency);
        }

        public static IPublisher<R> FlatMap<T, R>(this IPublisher<T> source, Func<T, IPublisher<R>> mapper, int bufferSize, bool delayError = false, int maxConcurrency = int.MaxValue)
        {
            if (source is ScalarSource<T>)
            {
                ScalarSource<T> scalar = (ScalarSource<T>)source;

                T t = scalar.Get();

                return Defer(() => mapper(t));
            }

            return Create<R>(s =>
            {
                PublisherFlatMap<T, R> parent = new PublisherFlatMap<T, R>(s, maxConcurrency, delayError, bufferSize, mapper);

                s.OnSubscribe(parent);

                source.Subscribe(parent);
            });
        }

        public static IPublisher<int> Range(int start, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count >= required but it was " + count);
            }
            if (count == 0)
            {
                return Empty<int>();
            }
            if (count == 1)
            {
                return Just<int>(start);
            }

            long end = ((long)start) + count;

            if (end > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("start + count overflows the integer range");
            }

            return Create<int>(s =>
            {
                s.OnSubscribe(new PublisherRange(start, end, s));
            });
        }

        public static IPublisher<bool> SequenceEqual<T>(this IPublisher<T> first, IPublisher<T> second)
        {
            return SequenceEqual(first, second, EqualityComparer<T>.Default);
        }

        public static IPublisher<bool> SequenceEqual<T>(this IPublisher<T> first, IPublisher<T> second, IEqualityComparer<T> comparer)
        {
            return Create<bool>(s =>
            {
                PublisherSequenceEqual<T> parent = new PublisherSequenceEqual<T>(s, BufferSize(), comparer);

                s.OnSubscribe(parent);

                parent.Subscribe(first, second);
            });
        }

        public static IPublisher<T> SwitchOnNext<T>(this IPublisher<IPublisher<T>> sources)
        {
            return sources.SwitchMap(v => v);
        }

        public static IPublisher<R> SwitchMap<T, R>(this IPublisher<T> source, Func<T, IPublisher<R>> mapper)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherSwitchMap<T, R>(s, mapper, BufferSize()));
            });
        }

        public static IPublisher<long> Timer(TimeSpan delay)
        {
            return Timer(delay, DefaultScheduler.Instance);
        }

        public static IPublisher<long> Timer(TimeSpan delay, IScheduler scheduler)
        {
            return Create<long>(s =>
            {
                PublisherTimer pt = new PublisherTimer(s);

                s.OnSubscribe(pt);

                pt.Set(scheduler.ScheduleDirect(pt.Signal, delay));
            });
        }

        public static IPublisher<T> Using<T, S>(Func<S> resourceSupplier, Func<S, IPublisher<T>> sourceFactory, Action<S> resourceDisposer, bool eager = true)
        {
            return Create<T>(s =>
            {
                S resource;

                try
                {
                    resource = resourceSupplier();
                }
                catch (Exception e)
                {
                    EmptySubscription.Error(s, e);
                    return;
                }
                IPublisher<T> p;

                try
                {
                    p = sourceFactory(resource);
                }
                catch (Exception e)
                {
                    try
                    {
                        resourceDisposer(resource);
                    }
                    catch (Exception ex)
                    {
                        e = new AggregateException(e, ex);
                    }

                    EmptySubscription.Error(s, e);
                    return;
                }

                if (p == null)
                {
                    Exception e = new NullReferenceException("The sourceFactory returned a null IPublisher");

                    try
                    {
                        resourceDisposer(resource);
                    }
                    catch (Exception ex)
                    {
                        e = new AggregateException(e, ex);
                    }

                    EmptySubscription.Error(s, e);
                    return;
                }

                p.Subscribe(new PublisherUsing<T, S>(s, resource, resourceDisposer, eager));
            });
        }

        public static IPublisher<R> Zip<T, R>(this IPublisher<T>[] sources, Func<T[], R> zipper)
        {
            return Zip(sources, zipper, BufferSize());
        }

        public static IPublisher<R> Zip<T, R>(this IPublisher<T>[] sources, Func<T[], R> zipper, int bufferSize)
        {
            int len = sources.Length;
            if (len == 0)
            {
                return Empty<R>();
            }
            else
            if (len == 1)
            {
                return sources[0].Map(v => zipper(new T[] { v }));
            }

            return Create<R>(s =>
            {
                int n = sources.Length;

                PublisherZip<T, R> zip = new PublisherZip<T, R>(s, n, zipper, bufferSize);

                s.OnSubscribe(zip);

                zip.Subscribe(sources, n);
            });
        }

        public static IPublisher<R> Zip<T, R>(this IEnumerable<IPublisher<T>> sources, Func<T[], R> zipper)
        {
            return Zip(sources, zipper, BufferSize());
        }

        public static IPublisher<R> Zip<T, R>(this IEnumerable<IPublisher<T>> sources, Func<T[], R> zipper, int bufferSize)
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
                else
                if (n == 1)
                {
                    a[0].Map(v => zipper(new T[] { v })).Subscribe(s);
                    return;
                }

                PublisherZip<T, R> zip = new PublisherZip<T, R>(s, n, zipper, bufferSize);

                s.OnSubscribe(zip);

                zip.Subscribe(a, n);
            });
        }

        public static IPublisher<R> Zip<T1, T2, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            Func<T1, T2, R> zipper)
        {
            return Zip(s1, s2, zipper, BufferSize());
        }
        public static IPublisher<R> Zip<T1, T2, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            Func<T1, T2, R> zipper, int bufferSize)
        {
            return Create<R>(s =>
            {
                PublisherZip2<T1, T2, R> zip = new PublisherZip2<T1, T2, R>(s, zipper, bufferSize);

                s.OnSubscribe(zip);

                zip.Subscribe(s1, s2);
            });
        }

        public static IPublisher<R> ZipWith<T1, T2, R>(this IPublisher<T1> source, IPublisher<T2> other, Func<T1, T2, R> zipper)
        {
            return Zip(source, other, zipper);
        }

        public static IPublisher<R> Zip<T1, T2, T3, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3,
            Func<T1, T2, T3, R> zipper)
        {
            // TODO requires custom implementation of PublisherZip
            throw new NotImplementedException();
        }

        public static IPublisher<R> Zip<T1, T2, T3, T4, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T2> s4,
            Func<T1, T2, T3, T4, R> zipper)
        {
            // TODO requires custom implementation of PublisherZip
            throw new NotImplementedException();
        }

        public static IPublisher<R> Zip<T1, T2, T3, T4, T5, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T2> s4,
            IPublisher<T5> s5,
            Func<T1, T2, T3, T4, T5, R> zipper)
        {
            // TODO requires custom implementation of PublisherZip
            throw new NotImplementedException();
        }

        public static IPublisher<R> Zip<T1, T2, T3, T4, T5, T6, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T2> s4,
            IPublisher<T5> s5, IPublisher<T2> s6,
            Func<T1, T2, T3, T4, T5, T6, R> zipper)
        {
            // TODO requires custom implementation of PublisherZip
            throw new NotImplementedException();
        }

        public static IPublisher<R> Zip<T1, T2, T3, T4, T5, T6, T7, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T2> s4,
            IPublisher<T5> s5, IPublisher<T2> s6,
            IPublisher<T7> s7,
            Func<T1, T2, T3, T4, T5, T6, T7, R> zipper)
        {
            // TODO requires custom implementation of PublisherZip
            throw new NotImplementedException();
        }


        public static IPublisher<R> Zip<T1, T2, T3, T4, T5, T6, T7, T8, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T2> s4,
            IPublisher<T5> s5, IPublisher<T2> s6,
            IPublisher<T7> s7, IPublisher<T2> s8,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, R> zipper)
        {
            // TODO requires custom implementation of PublisherZip
            throw new NotImplementedException();
        }

        public static IPublisher<R> Zip<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(
            IPublisher<T1> s1, IPublisher<T2> s2,
            IPublisher<T3> s3, IPublisher<T2> s4,
            IPublisher<T5> s5, IPublisher<T2> s6,
            IPublisher<T7> s7, IPublisher<T2> s8,
            IPublisher<T9> s9,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> zipper)
        {
            // TODO requires custom implementation of PublisherZip
            throw new NotImplementedException();
        }

        public static IPublisher<bool> All<T>(this IPublisher<T> source, Func<T, bool> predicate)
        {
            return Create<bool>(s =>
            {
                source.Subscribe(new PublisherAll<T>(s, predicate));
            });
        }

        public static IPublisher<T> AmbWith<T>(this IPublisher<T> source, IPublisher<T> other)
        {
            return AmbArray(source, other);
        }

        public static IPublisher<bool> Any<T>(this IPublisher<T> source, Func<T, bool> predicate)
        {
            return Create<bool>(s =>
            {
                source.Subscribe(new PublisherAny<T>(s, predicate));
            });
        }

        public static IPublisher<T> AsPublisher<T>(this IPublisher<T> source)
        {
            return Create<T>(s =>
            {
                source.Subscribe(s);
            });
        }

        public static IPublisher<C> Buffer<T, C>(this IPublisher<T> source, int size, int skip, Func<C> bufferFactory) where C : ICollection<T>
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException("size > 0 required but it was " + size);
            }
            if (skip <= 0)
            {
                throw new ArgumentOutOfRangeException("skip > 0 required but it was " + skip);
            }

            if (size == skip)
            {
                return Create<C>(s =>
                {
                    source.Subscribe(new PublisherBufferExact<T, C>(s, bufferFactory, size));
                });
            }
            if (size < skip)
            {
                return Create<C>(s =>
                {
                    source.Subscribe(new PublisherBufferSkip<T, C>(s, bufferFactory, size, skip));
                });
            }

            return Create<C>(s =>
            {
                source.Subscribe(new PublisherBufferOverlap<T, C>(s, bufferFactory, size, skip));
            });
        }

        public static IPublisher<R> Collect<T, R>(this IPublisher<T> source, Func<R> containerFactory, Action<R, T> collector)
        {
            return Create<R>(s =>
            {
                R c;

                try
                {
                    c = containerFactory();
                }
                catch (Exception e)
                {
                    EmptySubscription.Error(s, e);
                    return;
                }

                source.Subscribe(new PublisherCollect<T, R>(s, c, collector));
            });
        }

        public static IPublisher<R> Scan<T, R>(this IPublisher<T> source, R initialValue, Func<R, T, R> accumulator)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherScan<T, R>(s, initialValue, accumulator));
            });
        }

        public static IPublisher<R> Scan<T, R>(this IPublisher<T> source, Func<R> initialSupplier, Func<R, T, R> accumulator)
        {
            return Create<R>(s =>
            {
                R initialValue;

                try
                {
                    initialValue = initialSupplier();
                }
                catch (Exception e)
                {
                    EmptySubscription.Error(s, e);
                    return;
                }

                source.Subscribe(new PublisherScan<T, R>(s, initialValue, accumulator));
            });
        }

        public static IPublisher<T> Scan<T>(this IPublisher<T> source, Func<T, T, T> accumulator)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherScan<T>(s, accumulator));
            });
        }

        public static IPublisher<R> Reduce<T, R>(this IPublisher<T> source, R initialValue, Func<R, T, R> accumulator)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherReduce<T, R>(s, initialValue, accumulator));
            });
        }

        public static IPublisher<R> Reduce<T, R>(this IPublisher<T> source, Func<R> initialSupplier, Func<R, T, R> accumulator)
        {
            return Create<R>(s =>
            {
                R initialValue;

                try
                {
                    initialValue = initialSupplier();
                }
                catch (Exception e)
                {
                    EmptySubscription.Error(s, e);
                    return;
                }

                source.Subscribe(new PublisherScan<T, R>(s, initialValue, accumulator));
            });
        }

        public static IPublisher<T> Reduce<T>(this IPublisher<T> source, Func<T, T, T> accumulator)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherScan<T>(s, accumulator));
            });
        }

        public static IPublisher<long> Count<T>(this IPublisher<T> source)
        {
            return Create<long>(s =>
            {
                source.Subscribe(new PublisherCount<T>(s));
            });
        }

        public static IPublisher<bool> IsEmpty<T>(this IPublisher<T> source)
        {
            return Create<bool>(s =>
            {
                source.Subscribe(new PublisherIsEmpty<T>(s));
            });
        }

        public static IPublisher<T> Take<T>(this IPublisher<T> source, long n)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherTake<T>(s, n));
            });
        }

        public static IPublisher<T> Skip<T>(this IPublisher<T> source, long n)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException("n >= 0 required but it was " + n);
            }
            if (n == 0)
            {
                return source;
            }

            return Create<T>(s =>
            {
                source.Subscribe(new PublisherSkip<T>(s, n));
            });
        }

        public static IPublisher<T> IgnoreElements<T>(this IPublisher<T> source)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherIgnoreElements<T>(s));
            });
        }

        public static IPublisher<T> TakeLast<T>(this IPublisher<T> source, int n)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException("n >= 0 required but it was " + n);
            }
            if (n == 0)
            {
                return IgnoreElements(source);
            }
            if (n == 1)
            {
                return Create<T>(s =>
                {
                    source.Subscribe(new PublisherTakeLastOne<T>(s));
                });
            }
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherTakeLast<T>(s, n));
            });
        }

        public static IPublisher<T> SkipLast<T>(this IPublisher<T> source, int n)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException("n >= 0 required but it was " + n);
            }
            if (n == 0)
            {
                return source;
            }

            return Create<T>(s =>
            {
                source.Subscribe(new PublisherSkipLast<T>(s, n));
            });
        }

        public static IPublisher<T> TakeUntil<T, U>(this IPublisher<T> source, IPublisher<U> other)
        {
            return Create<T>(s =>
            {
                PublisherTakeUntil<T, U> main = new PublisherTakeUntil<T, U>(s);

                ISubscriber<U> su = main.CreateOther();

                s.OnSubscribe(main);

                other.Subscribe(su);

                source.Subscribe(main);
            });
        }

        public static IPublisher<T> SkipUntil<T, U>(this IPublisher<T> source, IPublisher<U> other)
        {
            return Create<T>(s =>
            {
                PublisherSkipUntil<T, U> main = new PublisherSkipUntil<T, U>(s);

                ISubscriber<U> su = main.CreateOther();

                s.OnSubscribe(main);

                other.Subscribe(su);

                source.Subscribe(main);
            });
        }

        public static IPublisher<T> SkipWhile<T>(this IPublisher<T> source, Func<T, bool> predicate)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherSkipWhile<T>(s, predicate));
            });
        }

        public static IPublisher<T> TakeWhile<T>(this IPublisher<T> source, Func<T, bool> predicate)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherTakeWhile<T>(s, predicate));
            });
        }

        public static IPublisher<T> TakeUntil<T>(this IPublisher<T> source, Func<T, bool> predicate)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherTakeUntil<T>(s, predicate));
            });
        }

        public static IPublisher<T> Delay<T>(this IPublisher<T> source, TimeSpan delay, bool delayError = false)
        {
            return Delay(source, delay, DefaultScheduler.Instance, delayError);
        }

        public static IPublisher<T> Delay<T>(this IPublisher<T> source, TimeSpan delay, IScheduler scheduler, bool delayError = false)
        {
            return Create<T>(s =>
            {
                if (delayError)
                {
                    source.Subscribe(new PublisherDelayFull<T>(s, scheduler.CreateWorker(), delay));
                }
                else
                {
                    source.Subscribe(new PublisherDelayNormal<T>(s, scheduler.CreateWorker(), delay));
                }
            });
        }

        public static IPublisher<T> DelaySubscription<T, U>(this IPublisher<T> source, IPublisher<U> other)
        {
            return Create<T>(s =>
            {
                other.Subscribe(new PublisherDelaySubscription<T, U>(s, source));
            });
        }

        public static IPublisher<T> DelaySubscription<T>(this IPublisher<T> source, TimeSpan delay)
        {
            return DelaySubscription<T>(source, delay, DefaultScheduler.Instance);
        }

        public static IPublisher<T> DelaySubscription<T>(this IPublisher<T> source, TimeSpan delay, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                PublisherDelaySubscriptionTimed<T> main = new PublisherDelaySubscriptionTimed<T>(s);

                s.OnSubscribe(main);

                main.Set(scheduler.ScheduleDirect(() =>
                {
                    source.Subscribe(main);
                }, delay));
            });
        }

        public static IPublisher<T> Delay<T, U>(this IPublisher<T> source, Func<T, IPublisher<U>> itemDelay)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherDelaySelector<T, U>(s, itemDelay));
            });
        }

        public static IPublisher<IList<T>> Buffer<T>(this IPublisher<T> source, TimeSpan timespan)
        {
            return Buffer(source, timespan, timespan, DefaultScheduler.Instance, () => new List<T>());
        }

        public static IPublisher<IList<T>> Buffer<T>(this IPublisher<T> source, TimeSpan timespan, IScheduler scheduler)
        {
            return Buffer(source, timespan, timespan, scheduler, () => new List<T>());
        }

        public static IPublisher<C> Buffer<T, C>(this IPublisher<T> source, TimeSpan timespan,
            TimeSpan timeskip, IScheduler scheduler, Func<C> bufferFactory) where C : ICollection<T>
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<IList<T>> Buffer<T, U>(this IPublisher<T> source, IPublisher<U> boundary)
        {
            return Buffer(source, boundary, () => new List<T>());
        }

        public static IPublisher<C> Buffer<T, U, C>(this IPublisher<T> source, IPublisher<U> boundary, Func<C> bufferFactory) where C : ICollection<T>
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<IList<T>> Buffer<T, U, V>(this IPublisher<T> source, IPublisher<U> bufferStart, Func<U, IPublisher<V>> bufferEnd)
        {
            return Buffer(source, bufferStart, bufferEnd, () => new List<T>());
        }

        public static IPublisher<C> Buffer<T, U, V, C>(this IPublisher<T> source, IPublisher<U> bufferStart, Func<U, IPublisher<V>> bufferEnd, Func<C> bufferFactory) where C : ICollection<T>
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<IList<T>> Buffer<T>(this IPublisher<T> source,
            TimeSpan timespan, int size, bool restartTimer = false)
        {
            return Buffer(source, timespan, DefaultScheduler.Instance, size, () => new List<T>(), restartTimer);
        }

        public static IPublisher<IList<T>> Buffer<T>(this IPublisher<T> source,
            TimeSpan timespan, IScheduler scheduler, int size, bool restartTimer = false)
        {
            return Buffer(source, timespan, scheduler, size, () => new List<T>(), restartTimer);
        }

        public static IPublisher<C> Buffer<T, C>(this IPublisher<T> source,
            TimeSpan timespan, int size, Func<C> bufferFactory, bool restartTimer = false) where C : ICollection<T>
        {
            return Buffer(source, timespan, DefaultScheduler.Instance, size, bufferFactory, restartTimer);
        }

        public static IPublisher<C> Buffer<T, C>(this IPublisher<T> source, TimeSpan timespan, IScheduler scheduler, int size, Func<C> bufferFactory, bool restartTimer = false) where C : ICollection<T>
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Cache<T>(this IPublisher<T> source)
        {
            return CacheWithCapacityHint(source, 16);
        }

        public static IPublisher<T> CacheWithCapacityHint<T>(this IPublisher<T> source, int capacityHint)
        {
            return new PublisherCache<T>(source, capacityHint);
        }

        public static IPublisher<T> ConcatWith<T>(this IPublisher<T> source, IPublisher<T> other)
        {
            return ConcatArray(source, other);
        }

        public static IPublisher<T> MergeWith<T>(this IPublisher<T> source, IPublisher<T> other)
        {
            return Merge(source, other);
        }

        public static IPublisher<bool> Contains<T>(this IPublisher<T> source, T value)
        {
            return Contains<T>(source, value, EqualityComparer<T>.Default);
        }

        public static IPublisher<bool> Contains<T>(this IPublisher<T> source, T value, IEqualityComparer<T> comparer)
        {
            return Any(source, v => comparer.Equals(v, value));
        }

        public static IPublisher<T> Debounce<T>(this IPublisher<T> source, TimeSpan timespan)
        {
            return Debounce(source, timespan, DefaultScheduler.Instance);
        }

        public static IPublisher<T> Debounce<T>(this IPublisher<T> source, TimeSpan timespan, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherDebounceTimed<T>(s, scheduler.CreateWorker(), timespan));
            });
        }

        public static IPublisher<T> Debounce<T, U>(this IPublisher<T> source, Func<T, IPublisher<U>> selector)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherDebounceSelector<T, U>(s, selector));
            });
        }

        public static IPublisher<T> DefaultIfEmpty<T>(this IPublisher<T> source, T value)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherDefaultIfEmpty<T>(s, value));
            });
        }

        public static IPublisher<T> SwitchIfEmpty<T>(this IPublisher<T> source, IPublisher<T> other)
        {
            return Create<T>(s =>
            {
                var parent = new PublisherSwitchIfEmpty<T>(s, other);

                s.OnSubscribe(parent);

                source.Subscribe(parent);
            });
        }

        public static IPublisher<T> Dematerialize<T>(this IPublisher<Signal<T>> source)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherDematerialize<T>(s));
            });
        }

        public static IPublisher<Signal<T>> Materialize<T>(this IPublisher<T> source)
        {
            return Create<Signal<T>>(s =>
            {
                source.Subscribe(new PublisherMaterialize<T>(s));
            });
        }

        public static IPublisher<T> Distinct<T>(this IPublisher<T> source)
        {
            return Distinct(source, v => v, EqualityComparer<T>.Default);
        }

        public static IPublisher<T> Distinct<T>(this IPublisher<T> source, IEqualityComparer<T> comparer)
        {
            return Distinct(source, v => v, comparer);
        }

        public static IPublisher<T> Distinct<T, K>(this IPublisher<T> source, Func<T, K> keyExtractor)
        {
            return Distinct(source, keyExtractor, EqualityComparer<K>.Default);
        }

        public static IPublisher<T> Distinct<T, K>(this IPublisher<T> source, Func<T, K> keyExtractor, IEqualityComparer<K> comparer)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherDistinct<T, K>(s, keyExtractor, comparer));
            });
        }

        public static IPublisher<T> DistinctUntilChanged<T>(this IPublisher<T> source)
        {
            return DistinctUntilChanged(source, v => v, EqualityComparer<T>.Default);
        }

        public static IPublisher<T> DistinctUntilChanged<T>(this IPublisher<T> source, IEqualityComparer<T> comparer)
        {
            return DistinctUntilChanged(source, v => v, comparer);
        }

        public static IPublisher<T> DistinctUntilChanged<T, K>(this IPublisher<T> source, Func<T, K> keyExtractor)
        {
            return DistinctUntilChanged(source, keyExtractor, EqualityComparer<K>.Default);
        }

        public static IPublisher<T> DistinctUntilChanged<T, K>(this IPublisher<T> source, Func<T, K> keyExtractor, IEqualityComparer<K> comparer)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherDistinctUntilChanged<T, K>(s, keyExtractor, comparer));
            });
        }

        public static IPublisher<T> Peek<T>(this IPublisher<T> source,
            Action<ISubscription> onSubscribeCall = null,
            Action<T> onNextCall = null,
            Action<Exception> onErrorCall = null,
            Action onCompleteCall = null,
            Action onAfterTerminateCall = null,
            Action<long> onRequestCall = null,
            Action onCancelCall = null
        )
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherPeek<T>(s, onSubscribeCall, onNextCall,
                    onErrorCall, onCompleteCall, onAfterTerminateCall,
                    onRequestCall, onCancelCall));
            });
        }

        public static IPublisher<R> ConcatMapEager<T, R>(this IPublisher<T> source,
            Func<T, IPublisher<R>> mapper)
        {
            return ConcatMapEager<T, R>(source, mapper, BufferSize());
        }

        public static IPublisher<R> ConcatMapEager<T, R>(this IPublisher<T> source,
            Func<T, IPublisher<R>> mapper, int bufferSize)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherConcatMapEager<T, R>(s, mapper, bufferSize));
            });
        }

        public static IPublisher<T> ConcatEager<T>(params IPublisher<T>[] sources)
        {
            return FromArray(sources).ConcatMapEager(v => v);
        }

        public static IPublisher<T> ConcatEager<T>(int bufferSize, params IPublisher<T>[] sources)
        {
            return FromArray(sources).ConcatMapEager(v => v, bufferSize);
        }

        public static IPublisher<T> ConcatEager<T>(this IEnumerable<IPublisher<T>> sources)
        {
            return FromEnumerable(sources).ConcatMapEager(v => v);
        }

        public static IPublisher<T> ConcatEager<T>(this IEnumerable<IPublisher<T>> sources, int bufferSize)
        {
            return FromEnumerable(sources).ConcatMapEager(v => v, bufferSize);
        }

        public static IPublisher<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources)
        {
            return sources.ConcatMapEager(v => v);
        }

        public static IPublisher<T> ConcatEager<T>(this IPublisher<IPublisher<T>> sources, int bufferSize)
        {
            return sources.ConcatMapEager(v => v, bufferSize);
        }

        public static IPublisher<T> ElementAt<T>(this IPublisher<T> source, long index)
        {
            if (index < 0L)
            {
                throw new ArgumentOutOfRangeException("index >= required but it was " + index);
            }
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherElementAt<T>(s, index));
            });
        }

        public static IPublisher<T> ElementAt<T>(this IPublisher<T> source, long index, T defaultValue)
        {
            if (index < 0L)
            {
                throw new ArgumentOutOfRangeException("index >= required but it was " + index);
            }
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherElementAtDefault<T>(s, index, defaultValue));
            });
        }

        public static IPublisher<T> Filter<T>(this IPublisher<T> source, Func<T, bool> predicate)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherFilter<T>(s, predicate));
            });
        }

        public static IPublisher<T> Single<T>(this IPublisher<T> source)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherSingle<T>(s));
            });
        }

        public static IPublisher<T> Single<T>(this IPublisher<T> source, T defaultValue)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherSingleDefault<T>(s, defaultValue));
            });
        }

        public static IPublisher<T> First<T>(this IPublisher<T> source)
        {
            return source.Take(1).Single();
        }

        public static IPublisher<T> First<T>(this IPublisher<T> source, T defaultValue)
        {
            return source.Take(1).Single(defaultValue);
        }

        public static IPublisher<R> FlatMap<T, R>(this IPublisher<T> source,
            Func<T, IPublisher<R>> onNext, Func<Exception, IPublisher<R>> onError,
            Func<IPublisher<R>> onComplete, bool delayError = false, int maxConcurrency = int.MaxValue)
        {
            return Create<IPublisher<R>>(s =>
            {
                source.Subscribe(new PublisherMapNotification<T, IPublisher<R>>(s, onNext, onError, onComplete));
            }).FlatMap(v => v, delayError, maxConcurrency);
        }

        public static IPublisher<R> FlatMap<T, U, R>(this IPublisher<T> source,
            Func<T, IPublisher<U>> mapper, Func<T, U, R> combiner, bool delayError = false, int maxConcurrency = int.MaxValue)
        {
            return source.FlatMap(t => mapper(t).Map(u => new Pair<T, U>(t, u)), delayError, maxConcurrency)
                .Map(tu => combiner(tu.first, tu.second));
        }

        struct Pair<T1, T2>
        {
            internal T1 first;
            internal T2 second;
            internal Pair(T1 t1, T2 t2)
            {
                first = t1;
                second = t2;
            }
        }

        public static IPublisher<R> FlatMap<T, R>(this IPublisher<T> source, Func<T, IEnumerable<R>> mapper, bool delayError = false, int maxConcurrency = int.MaxValue)
        {
            return source.FlatMap(v => FromEnumerable(mapper(v)), delayError, maxConcurrency);
        }

        public static IPublisher<R> FlatMap<T, U, R>(this IPublisher<T> source,
            Func<T, IEnumerable<U>> mapper, Func<T, U, R> combiner, bool delayError = false, int maxConcurrency = int.MaxValue)
        {
            return source.FlatMap(t => FromEnumerable(mapper(t)).Map(u => new Pair<T, U>(t, u)), delayError, maxConcurrency)
                .Map(tu => combiner(tu.first, tu.second));
        }

        public static IDisposable Subscribe<T>(this IPublisher<T> source) {
            LambdaSubscriber<T> ls = new LambdaSubscriber<T>(v => { });

            source.Subscribe(ls);

            return ls;
        }

        public static IDisposable Subscribe<T>(this IPublisher<T> source, Action<T> onNextCall)
        {
            LambdaSubscriber<T> ls = new LambdaSubscriber<T>(onNextCall);

            source.Subscribe(ls);

            return ls;
        }

        public static IDisposable Subscribe<T>(this IPublisher<T> source, Action<T> onNextCall,
            Action<Exception> onErrorCall)
        {
            LambdaSubscriber<T> ls = new LambdaSubscriber<T>(onNextCall, onErrorCall);

            source.Subscribe(ls);

            return ls;
        }

        public static IDisposable Subscribe<T>(this IPublisher<T> source, Action<T> onNextCall,
            Action<Exception> onErrorCall, Action onCompleteCall)
        {
            LambdaSubscriber<T> ls = new LambdaSubscriber<T>(onNextCall, onErrorCall, onCompleteCall);

            source.Subscribe(ls);

            return ls;
        }

        public static IDisposable Subscribe<T>(this IPublisher<T> source, Action<T> onNextCall,
            Action<Exception> onErrorCall, Action onCompleteCall, Action<ISubscription> onSubscribeCall)
        {
            LambdaSubscriber<T> ls = new LambdaSubscriber<T>(onNextCall, onErrorCall, onCompleteCall);

            source.Subscribe(ls);

            return ls;
        }

        public static IDisposable ForEach<T>(this IPublisher<T> source, Action<T> onNextCall)
        {
            return source.Subscribe(onNextCall);
        }

        public static IDisposable ForEach<T>(this IPublisher<T> source, Action<T> onNextCall,
            Action<Exception> onErrorCall)
        {
            return source.Subscribe(onNextCall, onErrorCall);
        }

        public static IDisposable ForEach<T>(this IPublisher<T> source, Action<T> onNextCall,
            Action<Exception> onErrorCall, Action onCompleteCall)
        {
            return source.Subscribe(onNextCall, onErrorCall, onCompleteCall);
        }

        public static IDisposable Connect<T>(this IConnectablePublisher<T> connectable)
        {
            IDisposable[] d = { EmptyDisposable.Instance };
            connectable.Connect(v => { d[0] = v; });
            return d[0];
        }

        public static IPublisher<IGroupedPublisher<K, T>> GroupBy<T, K>(this IPublisher<T> source, Func<T, K> keyExtractor)
        {
            return GroupBy(source, keyExtractor, v => v, EqualityComparer<K>.Default, BufferSize());
        }

        public static IPublisher<IGroupedPublisher<K, T>> GroupBy<T, K>(
            this IPublisher<T> source, Func<T, K> keyExtractor, IEqualityComparer<K> keyComparer)
        {
            return GroupBy(source, keyExtractor, v => v, keyComparer, BufferSize());
        }

        public static IPublisher<IGroupedPublisher<K, V>> GroupBy<T, K, V>(
            this IPublisher<T> source, Func<T, K> keyExtractor, Func<T, V> valueExtractor)
        {
            return GroupBy(source, keyExtractor, valueExtractor, EqualityComparer<K>.Default, BufferSize());
        }

        public static IPublisher<IGroupedPublisher<K, V>> GroupBy<T, K, V>(
            this IPublisher<T> source, Func<T, K> keyExtractor, Func<T, V> valueExtractor,
            IEqualityComparer<K> keyComparer)
        {
            return GroupBy(source, keyExtractor, valueExtractor, keyComparer, BufferSize());
        }

        public static IPublisher<IGroupedPublisher<K, V>> GroupBy<T, K, V>(this IPublisher<T> source,
            Func<T, K> keyExtractor, Func<T, V> valueExtractor,
            IEqualityComparer<K> keyComparer, int bufferSize)
        {
            return Create<IGroupedPublisher<K, V>>(s =>
            {
                source.Subscribe(new PublisherGroupBy<T, K, V>(s, keyExtractor, valueExtractor, bufferSize, keyComparer));
            });
        }

        public static IPublisher<T> Last<T>(this IPublisher<T> source)
        {
            return source.TakeLast(1).Single();
        }

        public static IPublisher<T> Last<T>(this IPublisher<T> source, T defaultValue)
        {
            return source.TakeLast(1).Single(defaultValue);
        }

        public static IPublisher<R> MapNotification<T, R>(this IPublisher<T> source,
            Func<T, R> onNext, Func<Exception, R> onError, Func<R> onComplete)
        {
            return Create<R>(s =>
            {
                source.Subscribe(new PublisherMapNotification<T, R>(s, onNext, onError, onComplete));
            });
        }

        public static IPublisher<T> ObserveOn<T>(this IPublisher<T> source, IScheduler scheduler, bool delayError = false)
        {
            return ObserveOn(source, scheduler, delayError, BufferSize());
        }

        public static IPublisher<T> ObserveOn<T>(this IPublisher<T> source, IScheduler scheduler, bool delayError, int bufferSize)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherObserveOn<T>(s, scheduler.CreateWorker(), delayError, bufferSize));
            });
        }

        public static IPublisher<T> OnBackpressureBufferAll<T>(this IPublisher<T> source)
        {
            return OnBackpressureBufferAll(source, BufferSize());
        }

        public static IPublisher<T> OnBackpressureBufferAll<T>(this IPublisher<T> source, int capacityHint)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherOnBackpressureBufferAll<T>(s, capacityHint));
            });
        }

        public static IPublisher<T> OnBackpressureBuffer<T>(this IPublisher<T> source, int maxCapacity, Action onOverflow = null)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherOnBackpressureBufferFixed<T>(s, maxCapacity, onOverflow));
            });
        }

        public static IPublisher<T> OnBackpressureDrop<T>(this IPublisher<T> source, Action<T> onDrop = null)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherOnBackpressureDrop<T>(s, onDrop));
            });
        }

        public static IPublisher<T> OnBackpressureLatest<T>(this IPublisher<T> source)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherOnBackpressureLatest<T>(s));
            });
        }

        public static IPublisher<T> OnErrorResumeNext<T>(this IPublisher<T> source, Func<Exception, IPublisher<T>> resumeFunction)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherOnErrorResumeNext<T>(s, resumeFunction));
            });
        }

        public static IPublisher<T> OnErrorResumeNext<T>(this IPublisher<T> source, IPublisher<T> other)
        {
            return OnErrorResumeNext(source, e => other);
        }

        public static IPublisher<T> OnErrorReturn<T>(this IPublisher<T> source, T defaultValue)
        {
            return OnErrorReturn(source, e => defaultValue);
        }

        public static IPublisher<T> OnErrorReturn<T>(this IPublisher<T> source, Func<Exception, T> resumeValue)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherOnErrorReturn<T>(s, resumeValue));
            });
        }

        public static IPublisher<R> Multicast<T, U, R>(this IPublisher<T> source,
    Func<IPublisher<T>, IConnectablePublisher<U>> connectableSupplier,
    Func<IPublisher<U>, IPublisher<R>> function)
        {
            return Create<R>(s =>
            {
                IConnectablePublisher<U> co;

                try
                {
                    co = connectableSupplier(source);
                }
                catch (Exception e)
                {
                    EmptySubscription.Error(s, e);
                    return;
                }

                if (co == null)
                {
                    EmptySubscription.Error(s, new NullReferenceException("The connectableSupplier returned null"));
                    return;
                }

                IPublisher<R> p;

                try
                {
                    p = function(co);
                }
                catch (Exception e)
                {
                    EmptySubscription.Error(s, e);
                    return;
                }

                if (p == null)
                {
                    EmptySubscription.Error(s, new NullReferenceException("The function returned a null Publisher"));
                    return;
                }

                var rs = new ResourceSubscriber<R>(s);

                p.Subscribe(rs);

                co.Connect(rs.Set);
            });
        }

        public static IConnectablePublisher<T> Publish<T>(this IPublisher<T> source)
        {
            return Publish(source, BufferSize());
        }

        public static IConnectablePublisher<T> Publish<T>(this IPublisher<T> source, int bufferSize)
        {
            return new PublisherPublish<T>(source, bufferSize);
        }

        public static IPublisher<R> Publish<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function)
        {
            return Publish(source, function, BufferSize());
        }

        public static IPublisher<R> Publish<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function, int bufferSize)
        {
            return Multicast(source, s => s.Publish(bufferSize), function);
        }

        public static IPublisher<T> AutoConnect<T>(this IConnectablePublisher<T> source)
        {
            return AutoConnect<T>(source, 1);
        }

        public static IPublisher<T> AutoConnect<T>(this IConnectablePublisher<T> source, int n)
        {
            return AutoConnect<T>(source, n, d => { });
        }

        public static IPublisher<T> AutoConnect<T>(this IConnectablePublisher<T> source, int n, Action<IDisposable> onConnect)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException("n >= 0 required but it was " + n);
            }
            else
            if (n == 0)
            {
                source.Connect(onConnect);
            }
            return new PublisherAutoConnect<T>(source, n, onConnect);
        }

        public static IPublisher<T> RefCount<T>(this IConnectablePublisher<T> source)
        {
            return new PublisherRefCount<T>(source);
        }

        public static IPublisher<T> Share<T>(this IPublisher<T> source)
        {
            return source.Publish().RefCount();
        }

        public static IPublisher<T> Repeat<T>(this IPublisher<T> source)
        {
            return Repeat(source, long.MaxValue);
        }

        public static IPublisher<T> Repeat<T>(this IPublisher<T> source, long times)
        {
            if (times < 0)
            {
                throw new ArgumentOutOfRangeException("times >= 0 required but it was " + times);
            }
            return Create<T>(s =>
            {
                PublisherRedo<T> parent = new PublisherRedo<T>(s, false, times, source);

                s.OnSubscribe(parent);

                parent.Resubscribe();
            });
        }

        public static IPublisher<T> Retry<T>(this IPublisher<T> source)
        {
            return Retry(source, long.MaxValue);
        }

        public static IPublisher<T> Retry<T>(this IPublisher<T> source, long times)
        {
            if (times < 0)
            {
                throw new ArgumentOutOfRangeException("times >= 0 required but it was " + times);
            }
            return Create<T>(s =>
            {
                PublisherRedo<T> parent = new PublisherRedo<T>(s, true, times, source);

                s.OnSubscribe(parent);

                parent.Resubscribe();
            });
        }

        public static IPublisher<T> RepeatWhen<T>(this IPublisher<T> source, Func<IPublisher<object>, IPublisher<object>> when)
        {
            return Create<T>(s =>
            {
                PublishProcessor<object> ps = new PublishProcessor<object>();

                IPublisher<object> p;

                try
                {
                    p = when(ps);
                }
                catch (Exception ex)
                {
                    EmptySubscription.Error(s, ex);
                    return;
                }

                if (p == null)
                {
                    EmptySubscription.Error(s, new NullReferenceException("The when returned a null Publisher"));
                    return;
                }

                PublisherRedoSignaller<T, object> signaller = new PublisherRedoSignaller<T, object>(s);

                PublisherRedoWhen<T> main = new PublisherRedoWhen<T>(s, source,
                    () => {
                        signaller.Request(1);

                        ps.OnNext(PublisherRedoWhen<T>.NEXT);
                    }, RxAdvancedFlowPlugins.OnError, signaller.Cancel, false);

                signaller.SetResubscribe(main.Resubscribe);

                s.OnSubscribe(main);

                p.Subscribe(signaller);

                main.Resubscribe();
            });
        }

        public static IPublisher<T> RetryWhen<T>(this IPublisher<T> source, Func<IPublisher<Exception>, IPublisher<object>> when)
        {
            return Create<T>(s =>
            {
                PublishProcessor<Exception> ps = new PublishProcessor<Exception>();

                IPublisher<object> p;

                try
                {
                    p = when(ps);
                }
                catch (Exception ex)
                {
                    EmptySubscription.Error(s, ex);
                    return;
                }

                if (p == null)
                {
                    EmptySubscription.Error(s, new NullReferenceException("The when returned a null Publisher"));
                    return;
                }

                PublisherRedoSignaller<T, object> signaller = new PublisherRedoSignaller<T, object>(s);

                PublisherRedoWhen<T> main = new PublisherRedoWhen<T>(s, source,
                    () => { }, e =>
                    {
                        signaller.Request(1);

                        ps.OnNext(e);
                    }, signaller.Cancel, false);

                signaller.SetResubscribe(main.Resubscribe);

                s.OnSubscribe(main);

                p.Subscribe(signaller);

                main.Resubscribe();
            });
        }

        public static IConnectablePublisher<T> Replay<T>(this IPublisher<T> source)
        {
            return ReplayAll(source, 16);
        }

        public static IConnectablePublisher<T> ReplayAll<T>(this IPublisher<T> source, int capacityHint)
        {
            return new PublisherReplay<T>(source, capacityHint);
        }

        public static IPublisher<R> Replay<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function)
        {
            return ReplayAll(source, function, 16);
        }

        public static IPublisher<R> ReplayAll<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function, int capacityHint)
        {
            return Multicast(source, s => s.ReplayAll(capacityHint), function);
        }

        public static IConnectablePublisher<T> Replay<T>(this IPublisher<T> source, int maxSize)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IConnectablePublisher<T> Replay<T>(this IPublisher<T> source, TimeSpan maxAge)
        {
            return Replay(source, maxAge, DefaultScheduler.Instance);
        }

        public static IConnectablePublisher<T> Replay<T>(this IPublisher<T> source, TimeSpan maxAge, IScheduler scheduler)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IConnectablePublisher<T> Replay<T>(this IPublisher<T> source, TimeSpan maxAge, int maxSize)
        {
            return Replay(source, maxAge, DefaultScheduler.Instance, maxSize);
        }

        public static IConnectablePublisher<T> Replay<T>(this IPublisher<T> source, TimeSpan maxAge, IScheduler scheduler, int maxSize)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<R> Replay<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function, int maxSize)
        {
            return Multicast(source, s => s.Replay(maxSize), function);
        }

        public static IPublisher<R> Replay<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function, TimeSpan maxAge)
        {
            return Multicast(source, s => s.Replay(maxAge), function);
        }

        public static IPublisher<R> Replay<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function, TimeSpan maxAge, IScheduler scheduler)
        {
            return Multicast(source, s => s.Replay(maxAge, scheduler), function);
        }

        public static IPublisher<R> Replay<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function, TimeSpan maxAge, int maxSize)
        {
            return Multicast(source, s => s.Replay(maxAge, maxSize), function);
        }

        public static IPublisher<R> Replay<T, R>(this IPublisher<T> source, Func<IPublisher<T>, IPublisher<R>> function, TimeSpan maxAge, IScheduler scheduler, int maxSize)
        {
            return Multicast(source, s => s.Replay(maxAge, scheduler, maxSize), function);
        }

        public static IPublisher<T> Retry<T>(this IPublisher<T> source, Func<Exception, bool> predicate)
        {
            return Create<T>(s =>
            {
                var op = new PublisherRetry<T>(s, predicate, long.MaxValue, source);

                s.OnSubscribe(op);

                op.Resubscribe();
            });
        }

        public static IPublisher<T> Sample<T>(this IPublisher<T> source, TimeSpan time)
        {
            return Sample(source, time, DefaultScheduler.Instance);
        }

        public static IPublisher<T> Sample<T>(this IPublisher<T> source, TimeSpan time, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                var op = new PublisherSampleMain<T>(s);

                var d = scheduler.SchedulePeriodicallyDirect(op.Run, time, time);

                op.Set(d);

                source.Subscribe(op);
            });
        }

        public static IPublisher<T> Sample<T, U>(this IPublisher<T> source, IPublisher<U> other)
        {
            return Create<T>(s =>
            {
                var op = new PublisherSampleMain<T>(s);

                var d = new PublisherSampleOther<T, U>(op);

                op.Set(d);

                other.Subscribe(d);

                source.Subscribe(op);
            });
        }

        public static IPublisher<T> Serialize<T>(this IPublisher<T> source)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new LockedSerializedSubscriber<T>(s));
            });
        }

        public static IPublisher<T> Skip<T>(this IPublisher<T> source, TimeSpan time)
        {
            return Skip<T>(source, time, DefaultScheduler.Instance);
        }

        public static IPublisher<T> Skip<T>(this IPublisher<T> source, TimeSpan time, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                var op = new PublisherSkipTimed<T>(s);

                var d = scheduler.ScheduleDirect(op.Run, time);

                op.Set(d);

                source.Subscribe(op);
            });
        }

        public static IPublisher<T> TakeLast<T>(this IPublisher<T> source, TimeSpan time)
        {
            return TakeLast(source, time, DefaultScheduler.Instance);
        }

        public static IPublisher<T> TakeLast<T>(this IPublisher<T> source, TimeSpan time, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherTakeLastTimed<T>(s, (long)time.TotalMilliseconds, scheduler));
            });
        }

        public static IPublisher<T> SkipLast<T>(this IPublisher<T> source, TimeSpan time)
        {
            return SkipLast(source, time, DefaultScheduler.Instance);
        }

        public static IPublisher<T> SkipLast<T>(this IPublisher<T> source, TimeSpan time, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherSkipLastTimed<T>(s, time, scheduler.CreateWorker()));
            });
        }

        public static IDisposable Subscribe<T>(this IPublisher<T> source, IObserver<T> observer)
        {
            var s = new ObserverSubscriber<T>(observer);

            source.Subscribe(s);

            return s;
        }

        public static IObservable<T> ToObservable<T>(this IPublisher<T> source)
        {
            return new ObservableFromFunc<T>(s =>
            {
                return source.Subscribe(s);
            });
        }

        public static IPublisher<T> OnBackpressureError<T>(this IPublisher<T> source)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherOnBackpressureError<T>(s));
            });
        }

        public static IPublisher<T> ToPublisher<T>(this IObservable<T> source, 
            BackpressureStrategy strategy = BackpressureStrategy.Buffer, 
            int bufferSize = int.MaxValue)
        {
            IPublisher<T> result = Create<T>(s =>
            {
                var o = new SubscriberObserver<T>(s);

                s.OnSubscribe(o);

                var d = source.Subscribe(o);

                o.Set(d);
            });

            switch (strategy)
            {
                case BackpressureStrategy.Buffer:
                    {
                        if (bufferSize == int.MaxValue)
                        {
                            return result.OnBackpressureBufferAll();
                        }
                        return result.OnBackpressureBuffer(bufferSize);
                    }
                case BackpressureStrategy.Drop:
                    {
                        return result.OnBackpressureDrop();
                    }
                case BackpressureStrategy.Latest:
                    {
                        return result.OnBackpressureLatest();
                    }
                default:
                    {
                        return result.OnBackpressureError();
                    }
            }
        }

        public static IPublisher<T> SubscribeOn<T>(this IPublisher<T> source, IScheduler scheduler, bool requestOn = true)
        {
            return Create<T>(s =>
            {
                if (requestOn)
                {
                    var op = new PublisherSubscribeOnRequest<T>(s, scheduler.CreateWorker());
                    s.OnSubscribe(op);

                    var d = scheduler.ScheduleDirect(() =>
                    {
                        source.Subscribe(op);
                    });
                }
                else
                {
                    var op = new PublisherSubscribeOn<T>(s);
                    s.OnSubscribe(op);

                    var d = scheduler.ScheduleDirect(() =>
                    {
                        op.Clear();
                        source.Subscribe(op);
                    });

                    op.Set(d);
                }

            });
        }

        public static IPublisher<T> ThrottleFirst<T>(this IPublisher<T> source, TimeSpan time)
        {
            return ThrottleFirst(source, time, DefaultScheduler.Instance);
        }

        public static IPublisher<T> ThrottleFirst<T>(this IPublisher<T> source, TimeSpan time, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherThrottleFirst<T>(s, scheduler.CreateWorker(), time));
            });
        }

        public static IPublisher<T> ThrottleLast<T>(this IPublisher<T> source, TimeSpan time)
        {
            return Sample(source, time);
        }

        public static IPublisher<T> ThrottleLast<T>(this IPublisher<T> source, TimeSpan time, IScheduler scheduler)
        {
            return Sample(source, time, scheduler);
        }

        public static IPublisher<T> ThrottleTimeout<T>(this IPublisher<T> source, TimeSpan time)
        {
            return Debounce(source, time);
        }

        public static IPublisher<T> ThrottleTimeout<T>(this IPublisher<T> source, TimeSpan time, IScheduler scheduler)
        {
            return Debounce(source, time, scheduler);
        }

        public static IPublisher<Timed<T>> TimeInterval<T>(this IPublisher<T> source)
        {
            return TimeInterval(source, DefaultScheduler.Instance);
        }

        public static IPublisher<Timed<T>> TimeInterval<T>(this IPublisher<T> source, IScheduler scheduler)
        {
            return Create<Timed<T>>(s =>
            {
                long[] currentTime = { scheduler.NowUtc() };

                source.Map(v =>
                {
                    long t = scheduler.NowUtc();
                    long e = currentTime[0];
                    currentTime[0] = t;

                    return new Timed<T>(v, t - e);
                }).Subscribe(s);

            });
        }

        public static IPublisher<Timed<T>> TimeStamped<T>(this IPublisher<T> source)
        {
            return TimeStamped(source, DefaultScheduler.Instance);
        }

        public static IPublisher<Timed<T>> TimeStamped<T>(this IPublisher<T> source, IScheduler scheduler)
        {
            return source.Map(v => new Timed<T>(v, scheduler.NowUtc()));
        }

        public static IPublisher<T> UnwrapTimed<T>(this IPublisher<Timed<T>> source)
        {
            return source.Map(t => t.value);
        }

        public static IPublisher<T> Timeout<T>(this IPublisher<T> source, TimeSpan time)
        {
            return Timeout(source, time, DefaultScheduler.Instance);
        }

        public static IPublisher<T> Timeout<T>(this IPublisher<T> source, TimeSpan time, 
            IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                var op = new PublisherTimeout<T>(s, scheduler.CreateWorker(), time, null);
                s.OnSubscribe(op);
                op.ScheduleFirst();
                source.Subscribe(op);
            });
        }

        public static IPublisher<T> Timeout<T>(this IPublisher<T> source, TimeSpan time, 
            IPublisher<T> other)
        {
            return Timeout(source, time, DefaultScheduler.Instance, other);
        }

        public static IPublisher<T> Timeout<T>(this IPublisher<T> source, TimeSpan time, 
            IScheduler scheduler, IPublisher<T> other)
        {
            return Create<T>(s =>
            {
                var op = new PublisherTimeout<T>(s, scheduler.CreateWorker(), time, other);
                s.OnSubscribe(op);
                op.ScheduleFirst();
                source.Subscribe(op);
            });
        }

        public static IPublisher<T> Timeout<T, U>(this IPublisher<T> source, 
            Func<T, IPublisher<U>> timeoutSupplier)
        {
            return Create<T>(s =>
            {
                var op = new PublisherTimeoutSelector<T, U, U>(s, timeoutSupplier, null);
                s.OnSubscribe(op);

                source.Subscribe(op);
            });
        }

        public static IPublisher<T> Timeout<T, U>(this IPublisher<T> source, 
            Func<T, IPublisher<U>> timeoutSupplier, IPublisher<T> other)
        {
            return Create<T>(s =>
            {
                var op = new PublisherTimeoutSelector<T, U, U>(s, timeoutSupplier, other);
                s.OnSubscribe(op);

                source.Subscribe(op);
            });
        }

        public static IPublisher<T> Timeout<T, U, V>(this IPublisher<T> source, 
            IPublisher<U> firstTimeout, Func<T, IPublisher<V>> timeoutSupplier)
        {
            return Create<T>(s =>
            {
                var op = new PublisherTimeoutSelector<T, U, V>(s, timeoutSupplier, null);
                s.OnSubscribe(op);

                op.SubscribeFirst(firstTimeout);

                source.Subscribe(op);
            });

        }

        public static IPublisher<T> Timeout<T, U, V>(this IPublisher<T> source, 
            IPublisher<U> firstTimeout, Func<T, IPublisher<V>> timeoutSupplier, 
            IPublisher<T> other)
        {
            return Create<T>(s =>
            {
                var op = new PublisherTimeoutSelector<T, U, V>(s, timeoutSupplier, other);
                s.OnSubscribe(op);

                op.SubscribeFirst(firstTimeout);

                source.Subscribe(op);
            });
        }

        public static IEnumerable<T> ToEnumerable<T>(this IPublisher<T> source)
        {
            return new PublisherToEnumerable<T>(source, BufferSize());
        }

        public static IEnumerable<T> ToEnumerable<T>(this IPublisher<T> source, int prefetch)
        {
            return new PublisherToEnumerable<T>(source, prefetch);
        }

        public static IPublisher<IList<T>> ToList<T>(this IPublisher<T> source)
        {
            return source.Collect(() => new List<T>(), (a, b) => a.Add(b));
        }

        public static IPublisher<IList<T>> ToList<T>(this IPublisher<T> source, int capacityHint)
        {
            return source.Collect(() => new List<T>(capacityHint), (a, b) => a.Add(b));
        }

        public static IPublisher<IDictionary<K, T>> ToDictionary<T, K>(this IPublisher<T> source,
            Func<T, K> keyExtractor)
        {
            return source.Collect(() => new Dictionary<K, T>(), (a, b) =>
            {
                K k = keyExtractor(b);
                if (!a.ContainsKey(k))
                {
                    a.Add(k, b);
                }
            });
        }

        public static IPublisher<IDictionary<K, T>> ToDictionary<T, K>(this IPublisher<T> source,
            Func<T, K> keyExtractor, IEqualityComparer<K> keyComparer)
        {
            return source.Collect(() => new Dictionary<K, T>(keyComparer), (a, b) =>
            {
                K k = keyExtractor(b);
                if (!a.ContainsKey(k))
                {
                    a.Add(k, b);
                }
            });
        }

        public static IPublisher<IDictionary<K, V>> ToDictionary<T, K, V>(this IPublisher<T> source,
            Func<T, K> keyExtractor, Func<T, V> valueExtractor)
        {
            return source.Collect(() => new Dictionary<K, V>(), (a, b) =>
            {
                K k = keyExtractor(b);
                if (!a.ContainsKey(k))
                {
                    a.Add(k, valueExtractor(b));
                }
            });
        }

        public static IPublisher<IDictionary<K, V>> ToDictionary<T, K, V>(this IPublisher<T> source,
            Func<T, K> keyExtractor, Func<T, V> valueExtractor, IEqualityComparer<K> keyComparer)
        {
            return source.Collect(() => new Dictionary<K, V>(keyComparer), (a, b) =>
            {
                K k = keyExtractor(b);
                if (!a.ContainsKey(k))
                {
                    a.Add(k, valueExtractor(b));
                }
            });
        }

        public static IPublisher<IList<T>> ToSortedList<T>(this IPublisher<T> source)
        {
            return Collect(source, () => new List<T>(), (a, b) => a.Add(b))
                .Map(v => { v.Sort(); return v; });
        }

        public static IPublisher<IList<T>> ToSortedList<T>(this IPublisher<T> source, int capacityHint)
        {
            return Collect(source, () => new List<T>(capacityHint), (a, b) => a.Add(b))
                .Map(v => { v.Sort(); return v; });
        }

        public static IPublisher<IList<T>> ToSortedList<T>(this IPublisher<T> source, Comparison<T> comparer)
        {
            return Collect(source, () => new List<T>(), (a, b) => a.Add(b))
                .Map(v => { v.Sort(comparer); return v; });
        }

        public static IPublisher<IList<T>> ToSortedList<T>(this IPublisher<T> source, Comparison<T> comparer, int capacityHint)
        {
            return Collect(source, () => new List<T>(capacityHint), (a, b) => a.Add(b))
                .Map(v => { v.Sort(comparer); return v; });
        }

        public static IPublisher<T> UnsubscribeOn<T>(this IPublisher<T> source, IScheduler scheduler)
        {
            return Create<T>(s =>
            {
                source.Subscribe(new PublisherUnsubscribeOn<T>(s, scheduler));
            });
        }

        public static IPublisher<R> WithLatestFrom<T, U, R>(this IPublisher<T> source, IPublisher<U> other, Func<T, U, R> combiner)
        {
            return Create<R>(s =>
            {
                var op = new PublisherWithLatestFrom<T, U, R>(s, combiner);
                s.OnSubscribe(op);

                other.Subscribe(op.GetOther());

                source.Subscribe(op);
            });
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, int size)
        {
            return Create<IPublisher<T>>(s =>
            {
                source.Subscribe(new PublisherWindowExact<T>(s, size));
            });
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, int size, int skip)
        {
            if (size == skip)
            {
                return Window(source, size);
            }
            else
            if (size < skip)
            {
                return Create<IPublisher<T>>(s =>
                {
                    source.Subscribe(new PublisherWindowSkip<T>(s, size, skip));
                });
            }
            return Create<IPublisher<T>>(s =>
            {
                source.Subscribe(new PublisherWindowOverlap<T>(s, size, skip));
            });
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan)
        {
            return Window(source, timespan, DefaultScheduler.Instance);
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan, TimeSpan timeskip)
        {
            return Window(source, timespan, timeskip, DefaultScheduler.Instance);
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan, IScheduler scheduler)
        {
            return Create<IPublisher<T>>(s =>
            {
                var op = new PublisherWindowTimedExact<T>(s, BufferSize(), timespan, scheduler);
                s.OnSubscribe(op);

                source.Subscribe(op);
            });
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan, TimeSpan timeskip, IScheduler scheduler)
        {
            if (timespan.Equals(timeskip))
            {
                return Window(source, timespan, scheduler);
            }
            if (timespan.CompareTo(timeskip) < 0)
            {
                return Create<IPublisher<T>>(s =>
                {
                    var op = new PublisherWindowTimedSkip<T>(s, timespan, timeskip, scheduler.CreateWorker(), BufferSize());

                    s.OnSubscribe(op);

                    source.Subscribe(op);
                });
            }
            return Create<IPublisher<T>>(s =>
            {
                var op = new PublisherWindowTimedOverlap<T>(s, timespan, timeskip, scheduler.CreateWorker(), BufferSize());

                s.OnSubscribe(op);

                source.Subscribe(op);
            });
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan, int size, bool restartTimer = false)
        {
            return Window(source, timespan, DefaultScheduler.Instance, size, restartTimer);
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan, IScheduler scheduler, int size, bool restartTimer = false)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan, TimeSpan timeskip, int size, bool restartTimer = false)
        {
            return Window(source, timespan, timeskip, DefaultScheduler.Instance, size, restartTimer);
        }

        public static IPublisher<IPublisher<T>> Window<T>(this IPublisher<T> source, 
            TimeSpan timespan, TimeSpan timeskip, IScheduler scheduler, 
            int size, bool restartTimer = false)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<IPublisher<T>> Window<T, B>(this IPublisher<T> source, 
            IPublisher<B> boundary)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<IPublisher<T>> Window<T, O, C>(this IPublisher<T> source, 
            IPublisher<O> openings, Func<O, IPublisher<C>> closings)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<R> ZipWith<T, U, R>(this IPublisher<T> source, IEnumerable<U> other, Func<T, U, R> zipper)
        {
            // TODO implement
            throw new NotImplementedException();
        }
    }
}