using System;

namespace RxAdvancedFlow
{
    public interface IScheduler
    {
        IDisposable ScheduleDirect(Action action);

        IDisposable ScheduleDirect(Action action, TimeSpan delay);

        IDisposable SchedulePeriodicallyDirect(Action action, TimeSpan initialDelay, TimeSpan period);

        IWorker CreateWorker();

        long NowUtc();

        void Start();

        void Shutdown();
    }

    public interface IWorker : IDisposable
    {
        IDisposable Schedule(Action action);

        IDisposable Schedule(Action action, TimeSpan delay);

        IDisposable SchedulePeriodically(Action action, TimeSpan initialDelay, TimeSpan period);

        long NowUtc();
    }
}
