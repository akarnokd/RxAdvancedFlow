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


    }
}
