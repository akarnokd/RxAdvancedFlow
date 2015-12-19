using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RxAdvancedFlow.internals.completable;
using RxAdvancedFlow.disposables;
using System.Threading;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.subscribers;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Extension methods for dealing with ICompletable sequences.
    /// </summary>
    public static class Completable
    {
        public static ICompletable Create(Action<ICompletableSubscriber> onSubscribe)
        {
            return new CompletableFromAction(onSubscribe);
        }

        public static ICompletable Lift(this ICompletable source, Func<ICompletableSubscriber, ICompletableSubscriber> onLift)
        {
            return new CompletableLift(source, onLift);
        }

        public static R To<R>(this ICompletable source, Func<ICompletable, R> converter)
        {
            return converter(source);
        }

        public static ICompletable Compose(this ICompletable source, Func<ICompletable, ICompletable> onCompose)
        {
            return To(source, onCompose);
        }

        public static IDisposable Subscribe<T>(this ICompletable source, IObserver<T> observer)
        {
            ObserverToCompletableSubscriber<T> s = new ObserverToCompletableSubscriber<T>(observer);

            source.Subscribe(s);

            return s;
        }

        public static ICompletable ToCompletable<T>(this IObservable<T> source)
        {
            return Create(cs =>
            {
                CompletableSubscriberToObserver<T> cso = new CompletableSubscriberToObserver<T>(cs);

                cs.OnSubscribe(cso);

                IDisposable d = source.Subscribe(cso);

                cso.Set(d);
            });
        }

        public static ICompletable ToCompletable<T>(this IPublisher<T> source)
        {
            return Create(cs =>
            {
                SubscriberToCompletableSubscriber<T> stcs = new SubscriberToCompletableSubscriber<T>(cs);

                source.Subscribe(stcs);
            });
        }

        public static ICompletable ToCompletable<T>(this ISingle<T> source)
        {
            return Create(cs =>
            {
                SingleSubscriberToCompletableSubscriber<T> sscs = new SingleSubscriberToCompletableSubscriber<T>(cs);

                source.Subscribe(sscs);
            });
        }

        public static ICompletable Amb(this ICompletable[] sources)
        {
            return Create(cs =>
            {
                AmbCompletableSubscriber acs = new AmbCompletableSubscriber(cs);

                foreach (ICompletable c in sources)
                {
                    if (acs.IsDisposed())
                    {
                        break;
                    }

                    c.Subscribe(acs);
                }
            });
        }

        public static ICompletable Amb(this IEnumerable<ICompletable> sources)
        {
            return Create(cs =>
            {
                AmbCompletableSubscriber acs = new AmbCompletableSubscriber(cs);

                foreach (ICompletable c in sources)
                {
                    if (acs.IsDisposed())
                    {
                        break;
                    }

                    c.Subscribe(acs);
                }
            });
        }

        public static ICompletable Merge(this ICompletable[] sources)
        {
            return Create(cs =>
            {
                MergeCompletableSubscriber mcs = new MergeCompletableSubscriber(cs);
                mcs.SpWip(sources.Length);

                foreach (ICompletable c in sources)
                {
                    if (mcs.IsDisposed() || mcs.LvWip() <= 0)
                    {
                        break;
                    }

                    c.Subscribe(mcs);
                }
            });
        }

        public static ICompletable Merge(this IEnumerable<ICompletable> sources)
        {
            return Create(cs =>
            {
                MergeCompletableSubscriber mcs = new MergeCompletableSubscriber(cs);
                mcs.SpWip(1);

                foreach (ICompletable c in sources)
                {
                    if (mcs.IsDisposed() || mcs.LvWip() <= 0)
                    {
                        break;
                    }

                    mcs.IncrementWip();
                    c.Subscribe(mcs);
                }

                mcs.OnComplete();
            });

        }

        public static ICompletable Defer(Func<ICompletable> factory)
        {
            return Create(cs =>
            {
                ICompletable c;

                try {
                    c = factory();
                } catch (Exception e)
                {
                    EmptyDisposable.Error(cs, e);
                    return;
                }

                c.Subscribe(cs);
            });
        }

        public static ICompletable FromAction(Action action)
        {
            return Create(cs =>
            {
                cs.OnSubscribe(EmptyDisposable.Empty);
                try
                {
                    action();
                } catch (Exception e)
                {
                    cs.OnError(e);
                    return;
                }
                cs.OnComplete();
            });
        }

        public static ICompletable Concat(this ICompletable[] sources)
        {
            return Concat(sources);
        }

        public static ICompletable Concat(this IEnumerable<ICompletable> sources)
        {
            return Create(cs =>
            {
                IEnumerator<ICompletable> it = sources.GetEnumerator();

                ConcatCompletableSubscriber ccs = new ConcatCompletableSubscriber(cs, it);

                cs.OnSubscribe(ccs);

                ccs.OnComplete();
            });
        }

        static readonly ICompletable EmptyCompletable = Create(cs =>
        {
            cs.OnSubscribe(EmptyDisposable.Empty);
            cs.OnComplete();
        });

        static readonly ICompletable NeverCompletable = Create(cs => cs.OnSubscribe(EmptyDisposable.Empty));

        public static ICompletable Empty()
        {
            return EmptyCompletable;
        }

        public static ICompletable Never()
        {
            return NeverCompletable;
        }

        public static ICompletable Throw(Exception e)
        {
            return Throw(() => e);
        }

        public static ICompletable Throw(Func<Exception> exceptionFactory)
        {
            return Create(cs =>
            {
                cs.OnSubscribe(EmptyDisposable.Empty);
                cs.OnError(exceptionFactory());
            });
        }

        public static ICompletable DoOnComplete(this ICompletable source, Action onCompleteCall)
        {
            return Create(cs =>
            {
                LambdaCompletableSubscriber lcs = new LambdaCompletableSubscriber(
                    cs, d => { }, onCompleteCall, e => { }, () => { });

                source.Subscribe(lcs);
            });
        }

        public static ICompletable DoOnError(this ICompletable source, Action<Exception> onErrorCall)
        {
            return Create(cs =>
            {
                LambdaCompletableSubscriber lcs = new LambdaCompletableSubscriber(
                    cs, d => { }, () => { }, onErrorCall, () => { });

                source.Subscribe(lcs);
            });
        }

        public static ICompletable DoAfterTerminate(this ICompletable source, Action onAfterTerminateCall)
        {
            return Create(cs =>
            {
                LambdaCompletableSubscriber lcs = new LambdaCompletableSubscriber(
                    cs, d => { }, () => { }, e => { }, onAfterTerminateCall);

                source.Subscribe(lcs);
            });
        }

        public static IDisposable Subscribe(this ICompletable source, Action onCompleteCall)
        {
            throw new NotImplementedException();
        }

        public static IDisposable Subscribe(this ICompletable source, Action onCompleteCall, Action<Exception> onErrorCall)
        {
            throw new NotImplementedException();
        }

        public static ICompletable OnErrorComplete(this ICompletable source)
        {
            throw new NotImplementedException();
        }

        public static ICompletable OnErrorResumeNext(this ICompletable source, Func<Exception, ICompletable> nextSelector)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Using<S>(
            Func<S> stateSupplier, 
            Func<S, ICompletable> completableFactory, 
            Action<S> stateDisposer,
            bool eager = true)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Repeat()
        {
            throw new NotImplementedException();
        }

        public static ICompletable Repeat(long times)
        {
            throw new NotImplementedException();
        }

        public static ICompletable RepeatUntil(Func<bool> shouldRepeat)
        {
            throw new NotImplementedException();
        }

        public static ICompletable RepeatWhen(Func<IObservable<object>, IObservable<object>> whenFunction)
        {
            throw new NotImplementedException();
        }

        public static IObservable<T> ToObservable<T>(this ICompletable source)
        {
            throw new NotImplementedException();
        }

        public static IPublisher<T> ToPublisher<T>(this ICompletable source)
        {
            throw new NotImplementedException();
        }

        public static ISingle<T> ToSingle<T>(this ICompletable source, T successValue)
        {
            throw new NotImplementedException();
        }

        public static ISingle<T> ToSingle<T>(this ICompletable source, Func<T> successValueSupplier)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Delay(this ICompletable source, TimeSpan time, bool delayError = false)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Delay(this ICompletable source, TimeSpan time, IScheduler scheduler, bool delayError = false)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Timeout(this ICompletable source, TimeSpan time)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Timeout(this ICompletable source, TimeSpan time, IScheduler scheduler)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Timeout(this ICompletable source, TimeSpan time, ICompletable next)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Timeout(this ICompletable source, TimeSpan time, IScheduler scheduler, ICompletable next)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Timer(TimeSpan time)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Timer(TimeSpan time, IScheduler scheduler)
        {
            throw new NotImplementedException();
        }

        public static ICompletable AndThen(this ICompletable source, ICompletable other)
        {
            throw new NotImplementedException();
        }

        public static ICompletable SubscribeOn(this ICompletable source, IScheduler scheduler)
        {

        }

        public static ICompletable ObserveOn(this ICompletable source, IScheduler scheduler)
        {

        }
    }


}
