using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    public interface IScheduler
    {
        IDisposable ScheduleDirect(Action action);

        IDisposable ScheduleDirect(Action action, TimeSpan delay);

        IDisposable SchedulePeriodicallyDirect(Action action, TimeSpan initialDelay, TimeSpan period);

        IWorker CreateWorker();

        long NowUtc();
    }

    public interface IWorker : IDisposable
    {
        IDisposable Schedule(Action action);

        IDisposable Schedule(Action action, TimeSpan delay);

        IDisposable Schedule(Action action, TimeSpan initialDelay, TimeSpan period);

        long NowUtc();
    }
}
