﻿using ReactiveStreamsCS;
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
using RxAdvancedFlow.internals;
using RxAdvancedFlow.subscribers;

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
            return Subscribe(source, onCompleteCall, e => RxAdvancedFlowPlugins.OnError(e));
        }

        public static IDisposable Subscribe(this ICompletable source, Action onCompleteCall, Action<Exception> onErrorCall)
        {
            CallbackCompletableSubscriber ccs = new CallbackCompletableSubscriber(onCompleteCall, onErrorCall);

            source.Subscribe(ccs);

            return ccs;
        }

        public static ICompletable OnErrorComplete(this ICompletable source)
        {
            return Create(cs =>
            {
                source.Subscribe(new OnErrorCompleteCompletableSubscriber(cs));
            });
        }

        public static ICompletable OnErrorResumeNext(this ICompletable source, ICompletable next)
        {
            return OnErrorResumeNext(source, e => next);
        }

        public static ICompletable OnErrorResumeNext(this ICompletable source, Func<Exception, ICompletable> nextSelector)
        {
            return Create(cs =>
            {
                source.Subscribe(new ResumeCompletableSubscriber(cs, nextSelector));
            });
        }

        public static ICompletable Using<S>(
            Func<S> stateSupplier, 
            Func<S, ICompletable> completableFactory, 
            Action<S> stateDisposer,
            bool eager = true)
        {
            return Create(cs =>
            {
                S state;

                try
                {
                    state = stateSupplier();
                }
                catch (Exception ex)
                {
                    EmptyDisposable.Error(cs, ex);
                    return;
                }

                ICompletable c;

                try
                {
                    c = completableFactory(state);
                }
                catch (Exception ex)
                {
                    EmptyDisposable.Error(cs, ex);
                    return;
                }

                c.Subscribe(new UsingCompletableSubscriber(cs, eager, () => stateDisposer(state)));
            });
        }

        public static ICompletable Repeat(this ICompletable source)
        {
            return Concat(new InfiniteRepeat<ICompletable>(source));
        }

        public static ICompletable Repeat(this ICompletable source, long times)
        {
            return Concat(InfiniteRepeat<ICompletable>.RepeatFinite(source, times));
        }

        public static ICompletable RepeatUntil(this ICompletable source, Func<bool> shouldRepeat)
        {
            return Concat(InfiniteRepeat<ICompletable>.PredicateRepeatUntil(source, shouldRepeat));
        }

        public static ICompletable RepeatWhen(this ICompletable source, Func<IObservable<object>, IObservable<object>> whenFunction)
        {
            throw new NotImplementedException();
        }

        public static ICompletable RepeatWhen(this ICompletable source, Func<IPublisher<object>, IPublisher<object>> whenFunction)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Retry(this ICompletable source)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Retry(this ICompletable source, long times)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Retry(this ICompletable source, Func<Exception, bool> retryIf)
        {
            throw new NotImplementedException();
        }

        public static ICompletable RetryWhen(this ICompletable source, Func<IObservable<Exception>, IObservable<object>> whenFunction)
        {
            throw new NotImplementedException();
        }

        public static ICompletable RetryWhen(this ICompletable source, Func<IPublisher<Exception>, IPublisher<object>> whenFunction)
        {
            throw new NotImplementedException();
        }

        public static IObservable<T> ToObservable<T>(this ICompletable source)
        {
            return new CompletableObservable<T>(source);
        }

        public static IPublisher<T> ToPublisher<T>(this ICompletable source)
        {
            return new CompletablePublisher<T>(source);
        }

        public static ISingle<T> ToSingle<T>(this ICompletable source, T successValue)
        {
            return ToSingle(source, () => successValue);
        }

        public static ISingle<T> ToSingle<T>(this ICompletable source, Func<T> successValueSupplier)
        {
            throw new NotImplementedException();
        }

        public static ICompletable Delay(this ICompletable source, TimeSpan time, bool delayError = false)
        {
            return Delay(source, time, DefaultScheduler.Instance, delayError);
        }

        public static ICompletable Delay(this ICompletable source, TimeSpan time, IScheduler scheduler, bool delayError = false)
        {
            return Create(cs =>
            {
                source.Subscribe(new DelayCompletableSubscriber(cs, scheduler, time, delayError));
            });
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
            return Timer(time, DefaultScheduler.Instance);
        }

        public static ICompletable Timer(TimeSpan time, IScheduler scheduler)
        {
            return Create(cs =>
            {
                MultipleAssignmentDisposable mad = new MultipleAssignmentDisposable();
                cs.OnSubscribe(mad);
                if (!mad.IsDisposed())
                {
                    mad.Set(scheduler.ScheduleDirect(() => cs.OnComplete()));
                }
            });
        }

        public static ICompletable AndThen(this ICompletable source, ICompletable other)
        {
            throw new NotImplementedException();
        }

        public static IObservable<T> AndThen<T>(this ICompletable source, IObservable<T> other)
        {
            throw new NotImplementedException();
        }

        public static IPublisher<T> AndThen<T>(this ICompletable source, IPublisher<T> other)
        {
            throw new NotImplementedException();
        }

        public static ISingle<T> AndThen<T>(this ICompletable source, ISingle<T> other)
        {
            throw new NotImplementedException();
        }

        public static ICompletable AndThen<T>(this IObservable<T> source, ICompletable other)
        {
            return source.ToCompletable().AndThen(other);
        }

        public static ICompletable AndThen<T>(this IPublisher<T> source, ICompletable other)
        {
            return source.ToCompletable().AndThen(other);
        }

        public static ICompletable AndThen<T>(this ISingle<T> source, ICompletable other)
        {
            return source.ToCompletable().AndThen(other);
        }

        public static ICompletable SubscribeOn(this ICompletable source, IScheduler scheduler)
        {
            return Create(cs =>
            {
                MultipleAssignmentDisposable inner = new MultipleAssignmentDisposable();

                MultipleAssignmentDisposable outer = new MultipleAssignmentDisposable(inner);

                cs.OnSubscribe(outer);

                inner.Set(scheduler.ScheduleDirect(() =>
                {
                    source.Subscribe(new SubscribeOnCompletableSubscriber(cs, outer));
                }));

            });
        }

        public static ICompletable ObserveOn(this ICompletable source, IScheduler scheduler)
        {
            return Create(cs =>
            {
                ObserveOnCompletableSubscriber oocs = new ObserveOnCompletableSubscriber(cs, scheduler);

                source.Subscribe(oocs);
            });
        }

        public static ICompletable UnsubscribeOn(this ICompletable source, IScheduler scheduler)
        {
            return Create(cs =>
            {
                UnsubscribeOnCompletableSubscriber oocs = new UnsubscribeOnCompletableSubscriber(cs, scheduler);

                source.Subscribe(oocs);
            });
        }

        public static void Await(this ICompletable source)
        {
            LatchedCompletableSubscriber lcs = new LatchedCompletableSubscriber();

            source.Subscribe(lcs);

            lcs.Await();
        }

        public static bool Await(this ICompletable source, TimeSpan timeout)
        {
            LatchedCompletableSubscriber lcs = new LatchedCompletableSubscriber();

            source.Subscribe(lcs);

            return lcs.Await(timeout);
        }
    }


}
