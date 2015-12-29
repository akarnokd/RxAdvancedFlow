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
            return Create<R>(s => {
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
    }
}
