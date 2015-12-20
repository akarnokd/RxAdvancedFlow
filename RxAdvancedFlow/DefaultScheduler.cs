using RxAdvancedFlow.disposables;
using RxAdvancedFlow.internals.disposables;
using RxAdvancedFlow.internals.schedulers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    public static class DefaultScheduler
    {

        public static IScheduler Instance = new ThreadPoolScheduler();

        sealed class ThreadPoolScheduler : IScheduler
        {
            internal static DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

            public IWorker CreateWorker()
            {
                return new ThreadPoolWorker();
            }

            public long NowUtc()
            {
                return (long)(DateTimeOffset.UtcNow - Epoch).TotalMilliseconds;
            }

            public IDisposable ScheduleDirect(Action action)
            {
                return Task.Run(action);
            }

            public IDisposable ScheduleDirect(Action action, TimeSpan delay)
            {
                return Task.Delay(delay).ContinueWith(a => action());
            }

            public IDisposable SchedulePeriodicallyDirect(Action action, TimeSpan initialDelay, TimeSpan period)
            {

                VersionedSoloDisposable vsd = new VersionedSoloDisposable();

                long startTime = NowUtc() + (long)initialDelay.TotalMilliseconds;

                long periodMillis = (long)period.TotalMilliseconds;

                long[] count = new long[1];

                Action nextAction = null;
                nextAction = () =>
                {
                    if (!vsd.IsDisposed())
                    {
                        action();

                        long now = NowUtc();

                        long c = ++count[0];
                        long next = startTime + c * periodMillis;

                        long toDelay = Math.Max(0, next - startTime);

                        vsd.Set(c, Task.Delay(TimeSpan.FromMilliseconds(toDelay)).ContinueWith(a => nextAction()));
                    };
                };

                vsd.Set(0, Task.Delay(initialDelay).ContinueWith(a => nextAction()));

                return vsd;
            }

            public void Shutdown()
            {
                // No op for the default threadpool
            }

            public void Start()
            {
                // No op for the default threadpool
            }

            sealed class ThreadPoolWorker : IWorker
            {
                readonly SetCompositeDisposable tasks;
                readonly ConcurrentQueue<ScheduledAction> queue;

                int wip;

                public ThreadPoolWorker()
                {
                    tasks = new SetCompositeDisposable();
                    queue = new ConcurrentQueue<ScheduledAction>();
                }

                public void Dispose()
                {
                    tasks.Dispose();
                }

                public long NowUtc()
                {
                    return (long)(DateTimeOffset.UtcNow - Epoch).TotalMilliseconds;
                }

                public IDisposable Schedule(Action action)
                {
                    ScheduledAction sa = new ScheduledAction(action, tasks);
                    if (tasks.Add(sa))
                    {
                        queue.Enqueue(sa);
                        if (Interlocked.Increment(ref wip) == 1)
                        {
                            Task.Run(() => Run());
                        }

                        return sa;
                    }
                    return EmptyDisposable.Instance;
                }

                public IDisposable Schedule(Action action, TimeSpan delay)
                {
                    MultipleAssignmentDisposable inner = new MultipleAssignmentDisposable();

                    MultipleAssignmentDisposable outer = new MultipleAssignmentDisposable(inner);

                    if (tasks.Add(outer)) {

                        IDisposable cancel = new ActionWeakDisposable(() => tasks.Remove(outer));

                        IDisposable f = Task.Delay(delay).ContinueWith(a =>
                        {
                            if (!outer.IsDisposed())
                            {
                                outer.Set(Schedule(action));
                            }
                        });

                        inner.Set(f);

                        return cancel;
                    }
                    return EmptyDisposable.Instance;
                }

                public IDisposable SchedulePeriodically(Action action, TimeSpan initialDelay, TimeSpan period)
                {
                    MultipleAssignmentDisposable inner = new MultipleAssignmentDisposable();

                    MultipleAssignmentDisposable outer = new MultipleAssignmentDisposable(inner);

                    if (tasks.Add(outer))
                    {

                        IDisposable cancel = new ActionWeakDisposable(() => tasks.Remove(outer));

                        long startTime = NowUtc() + (long)initialDelay.TotalMilliseconds;

                        long periodMillis = (long)period.TotalMilliseconds;

                        long[] count = new long[1];

                        Action nextAction = null;

                        nextAction = () =>
                        {
                            if (!outer.IsDisposed())
                            {
                                action();

                                long now = NowUtc();

                                long c = ++count[0];
                                long next = startTime + c * periodMillis;

                                long toDelay = Math.Max(0, next - startTime);

                                outer.Set(Schedule(nextAction, TimeSpan.FromMilliseconds(toDelay)));
                            }
                        };

                        IDisposable f = Task.Delay(initialDelay).ContinueWith(a =>
                        {
                            if (!outer.IsDisposed())
                            {
                                outer.Set(Schedule(nextAction));
                            }
                        });

                        inner.Set(f);

                        return cancel;
                    }
                    return EmptyDisposable.Instance;
                }

                public void Run()
                {
                    int missed = 1;

                    for (;;)
                    {

                        for (;;)
                        {
                            if (tasks.IsDisposed())
                            {
                                return;
                            }

                            ScheduledAction sa;

                            if (!queue.TryDequeue(out sa))
                            {
                                break;
                            }

                            sa.Run();
                        }

                        missed = Interlocked.Add(ref wip, -missed);
                        if (missed == 0)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
}
