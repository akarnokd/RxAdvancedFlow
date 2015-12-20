using ReactiveStreamsCS;
using RxAdvancedFlow.disposables;
using RxAdvancedFlow.internals;
using RxAdvancedFlow.internals.completable;
using RxAdvancedFlow.internals.disposables;
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

        public static ISingle<T> Just<T>(T value)
        {
            return new ScalarSource<T>(value);
        }

        public static ISingle<T> Never<T>()
        {
            // I don't think the C# typesystem allows this to be a static instance
            // because (ISingle<T>)ISingle<object> fails.
            return Create<T>(s =>
            {
                s.OnSubscribe(EmptyDisposable.Instance);
            });
        }

        public static ISingle<T> FromFunction<T>(Func<T> supplier)
        {
            return Create<T>(s =>
            {
                T v;

                try
                {
                    v = supplier();
                } catch (Exception e)
                {
                    s.OnError(e);
                    return;
                }
                s.OnSubscribe(EmptyDisposable.Instance);
                s.OnSuccess(v);
            });
        }

        public static ISingle<T> Throw<T>(Exception error)
        {
            return Throw<T>(() => error);
        }

        public static ISingle<T> Throw<T>(Func<Exception> errorSupplier)
        {
            return Create<T>(s =>
            {
                Exception e;

                try
                {
                    e = errorSupplier();
                }
                catch (Exception ex)
                {
                    EmptyDisposable.Error(s, ex);
                    return;
                }
                EmptyDisposable.Error(s, e);
            });
        }

        public static IObservable<T> ToObservable<T>(this ISingle<T> source)
        {
            return new SingleToObservable<T>(source);
        }

        public static IDisposable Subscribe<T>(this ISingle<T> source, IObserver<T> observer)
        {
            ObserverToSingleSubscriber<T> oss = new ObserverToSingleSubscriber<T>(observer);

            source.Subscribe(oss);

            return oss;
        }

        public static void Subscribe<T>(this ISingle<T> source, ISubscriber<T> subscriber)
        {
            source.Subscribe(new SubscriberToSingleSubscriber<T>(subscriber));
        }

        public static IPublisher<T> ToPublisher<T>(this ISingle<T> source)
        {
            if (source is ScalarSource<T>)
            {
                return (ScalarSource<T>)source;
            }

            return Flowable.Create<T>(s =>
            {
                source.Subscribe(new SubscriberToSingleSubscriber<T>(s));
            });
        }

        /// <summary>
        /// Creates the C# equivalent of a NoSuchElementException which is
        /// an InvalidOperationException with message
        /// "Sequence contains no elements".
        /// </summary>
        /// <returns></returns>
        internal static InvalidOperationException NoSuchElementException()
        {
            return new InvalidOperationException("Sequence contains no elements");
        }

        public static ISingle<T> Amb<T>(this ISingle<T>[] sources)
        {
            if (sources.Length == 0)
            {
                return Throw<T>(() => NoSuchElementException());
            }

            return Create<T>(s =>
            {
                AmbSingleSubscriber<T> ambs = new AmbSingleSubscriber<T>(s);

                s.OnSubscribe(ambs);

                foreach (ISingle<T> a in sources)
                {
                    if (ambs.IsDisposed())
                    {
                        break;
                    }

                    a.Subscribe(ambs);
                }
            });
        }

        public static ISingle<T> Amb<T>(this IEnumerable<ISingle<T>> sources)
        {
            return Create<T>(s =>
            {
                AmbSingleSubscriber<T> ambs = new AmbSingleSubscriber<T>(s);

                s.OnSubscribe(ambs);

                int c = 0;
                foreach (ISingle<T> a in sources)
                {
                    if (ambs.IsDisposed())
                    {
                        break;
                    }

                    a.Subscribe(ambs);
                    c++;
                }
                if (c == 0 && !ambs.IsDisposed())
                {
                    s.OnError(NoSuchElementException());
                }
            });

        }

        public static IPublisher<T> Concat<T>(this ISingle<T>[] sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Concat<T>(this IEnumerable<ISingle<T>> sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Concat<T>(this IObservable<ISingle<T>> sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Concat<T>(this IPublisher<ISingle<T>> sources)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Merge<T>(this ISingle<T>[] sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Merge<T>(this IEnumerable<ISingle<T>> sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Merge<T>(this IObservable<ISingle<T>> sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> Merge<T>(this IPublisher<ISingle<T>> sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> MergeDelayError<T>(this ISingle<T>[] sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> MergeDelayError<T>(this IEnumerable<ISingle<T>> sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> MergeDelayError<T>(this IObservable<ISingle<T>> sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static IPublisher<T> MergeDelayError<T>(this IPublisher<ISingle<T>> sources, int maxConcurrency = int.MaxValue)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static ISingle<T> Defer<T>(Func<ISingle<T>> singleSupplier)
        {
            return Create<T>(s =>
            {
                ISingle<T> single;

                try {
                    single = singleSupplier();
                } catch (Exception e)
                {
                    EmptyDisposable.Error(s, e);
                    return;
                }
                single.Subscribe(s);
            });
        }

        public static ISingle<T> ToSingle<T>(this Task<T> task)
        {
            return Create<T>(s =>
            {
                MultipleAssignmentDisposable mad = new MultipleAssignmentDisposable();

                s.OnSubscribe(mad);

                mad.Set(task.ContinueWith(t =>
                {
                    Exception e = t.Exception;
                    if (e != null)
                    {
                        s.OnError(e);
                    }
                    else
                    {
                        s.OnSuccess(t.Result);
                    }
                }));
            });
        }

        public static ISingle<T> Merge<T>(this ISingle<ISingle<T>> source)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static ISingle<long> Timer(TimeSpan delay)
        {
            return Timer(delay, DefaultScheduler.Instance);
        }

        public static ISingle<long> Timer(TimeSpan delay, IScheduler scheduler)
        {
            return Create<long>(s =>
            {
                MultipleAssignmentDisposable mad = new MultipleAssignmentDisposable();

                s.OnSubscribe(mad);

                mad.Set(scheduler.ScheduleDirect(() => s.OnSuccess(0), delay));
            });
        }

        public static ISingle<bool> Equals<T>(this ISingle<T> first, ISingle<T> second)
        {
            return Create<bool>(s =>
            {
                T[] array = new T[2];
                int[] counter = { 2 };
                SetCompositeDisposable all = new SetCompositeDisposable();

                s.OnSubscribe(all);

                first.Subscribe(new EqualsSingleSubscriber<T>(s, 0, array, counter, all));
                second.Subscribe(new EqualsSingleSubscriber<T>(s, 1, array, counter, all));
            });
        }

        public static ISingle<T> Using<T, S>(Func<S> stateSupplier, Func<S, ISingle<T>> singleFactory, Action<S> stateDisposer, bool eager = false)
        {
            // TODO implement
            throw new NotImplementedException();
        }

        public static ISingle<R> Zip<T, R>(this ISingle<T>[] sources, Func<T[], R> zipper)
        {
            if (sources.Length == 0)
            {
                return Throw<R>(() => NoSuchElementException());
            }
            return Create<R>(s =>
            {
                int n = sources.Length;

                T[] array = new T[n];
                int[] counter = { n };

                SetCompositeDisposable all = new SetCompositeDisposable();

                s.OnSubscribe(all);

                for (int i = 0; i < n; i++)
                {
                    if (all.IsDisposed())
                    {
                        return;
                    }

                    sources[i].Subscribe(new ZipSingleSubscriber<T, R>(s, i, array, counter, all, zipper));
                }
            });
        }

        public static ISingle<R> Zip<T, R>(this IEnumerable<ISingle<T>> sources, Func<T[], R> zipper)
        {
            return Create<R>(s =>
            {
                int n = 0;

                ISingle<T>[] a = new ISingle<T>[8];
                
                foreach (ISingle<T> source in sources) {
                    if (n == a.Length)
                    {
                        ISingle<T>[] b = new ISingle<T>[n + (n >> 2)];
                        Array.Copy(a, 0, b, 0, n);
                        a = b;
                    }
                    a[n++] = source;
                }

                if (n == 0)
                {
                    EmptyDisposable.Error(s, NoSuchElementException());
                    return;
                }

                T[] array = new T[n];
                int[] counter = { n };

                SetCompositeDisposable all = new SetCompositeDisposable();

                s.OnSubscribe(all);

                for (int i = 0; i < n; i++)
                {
                    if (all.IsDisposed())
                    {
                        return;
                    }

                    a[i].Subscribe(new ZipSingleSubscriber<T, R>(s, i, array, counter, all, zipper));
                }
            });
        }

        public static ISingle<R> Zip<T1, T2, R>(
            ISingle<T1> s1, ISingle<T2> s2, 
            Func<T1, T2, R> zipper)
        {
            return Zip(
                new ISingle<object>[] { (ISingle<object>)s1, (ISingle<object>)s2 }, 
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<R> Zip<T1, T2, T3, R>(
            ISingle<T1> s1, ISingle<T2> s2,
            ISingle<T3> s3,
            Func<T1, T2, T3, R> zipper)
        {
            return Zip(
                new ISingle<object>[] {
                    (ISingle<object>)s1, (ISingle<object>)s2,
                    (ISingle<object>)s3
                },
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<R> Zip<T1, T2, T3, T4, R>(
            ISingle<T1> s1, ISingle<T2> s2,
            ISingle<T3> s3, ISingle<T4> s4,
            Func<T1, T2, T3, T4, R> zipper)
        {
            return Zip(
                new ISingle<object>[] {
                    (ISingle<object>)s1, (ISingle<object>)s2,
                    (ISingle<object>)s3, (ISingle<object>)s4,
                },
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<R> Zip<T1, T2, T3, T4, T5, R>(
            ISingle<T1> s1, ISingle<T2> s2,
            ISingle<T3> s3, ISingle<T4> s4,
            ISingle<T5> s5,
            Func<T1, T2, T3, T4, T5, R> zipper)
        {
            return Zip(
                new ISingle<object>[] {
                    (ISingle<object>)s1, (ISingle<object>)s2,
                    (ISingle<object>)s3, (ISingle<object>)s4,
                    (ISingle<object>)s5
                },
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<R> Zip<T1, T2, T3, T4, T5, T6, R>(
            ISingle<T1> s1, ISingle<T2> s2,
            ISingle<T3> s3, ISingle<T4> s4,
            ISingle<T5> s5, ISingle<T6> s6,
            Func<T1, T2, T3, T4, T5, T6, R> zipper)
        {
            return Zip(
                new ISingle<object>[] {
                    (ISingle<object>)s1, (ISingle<object>)s2,
                    (ISingle<object>)s3, (ISingle<object>)s4,
                    (ISingle<object>)s5, (ISingle<object>)s6
                },
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<R> Zip<T1, T2, T3, T4, T5, T6, T7, R>(
            ISingle<T1> s1, ISingle<T2> s2,
            ISingle<T3> s3, ISingle<T4> s4,
            ISingle<T5> s5, ISingle<T6> s6,
            ISingle<T7> s7,
            Func<T1, T2, T3, T4, T5, T6, T7, R> zipper)
        {
            return Zip(
                new ISingle<object>[] {
                    (ISingle<object>)s1, (ISingle<object>)s2,
                    (ISingle<object>)s3, (ISingle<object>)s4,
                    (ISingle<object>)s5, (ISingle<object>)s6,
                    (ISingle<object>)s7
                },
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<R> Zip<T1, T2, T3, T4, T5, T6, T7, T8, R>(
            ISingle<T1> s1, ISingle<T2> s2,
            ISingle<T3> s3, ISingle<T4> s4,
            ISingle<T5> s5, ISingle<T6> s6,
            ISingle<T7> s7, ISingle<T8> s8,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, R> zipper)
        {
            return Zip(
                new ISingle<object>[] {
                    (ISingle<object>)s1, (ISingle<object>)s2,
                    (ISingle<object>)s3, (ISingle<object>)s4,
                    (ISingle<object>)s5, (ISingle<object>)s6,
                    (ISingle<object>)s7, (ISingle<object>)s8
                },
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<R> Zip<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(
            ISingle<T1> s1, ISingle<T2> s2,
            ISingle<T3> s3, ISingle<T4> s4,
            ISingle<T5> s5, ISingle<T6> s6,
            ISingle<T7> s7, ISingle<T8> s8,
            ISingle<T9> s9,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> zipper)
        {
            return Zip(
                new ISingle<object>[] {
                    (ISingle<object>)s1, (ISingle<object>)s2,
                    (ISingle<object>)s3, (ISingle<object>)s4,
                    (ISingle<object>)s5, (ISingle<object>)s6,
                    (ISingle<object>)s7, (ISingle<object>)s8,
                    (ISingle<object>)s9
                },
                LambdaHelper.ToFuncN(zipper));
        }

        public static ISingle<T> AmbWith<T>(this ISingle<T> source, ISingle<T> other)
        {
            return Amb<T>(new ISingle<T>[] { source, other });
        }

        public static ISingle<T> AsSingle<T>(this ISingle<T> source)
        {
            return Create<T>(s => source.Subscribe(s));
        }

        public static ISingle<T> Cache<T>(this ISingle<T> source)
        {
            return new SingleCache<T>(source);
        }
    }
}
