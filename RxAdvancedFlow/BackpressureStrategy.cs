using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Indicates what backpressure strategy to use when dealing with
    /// standard IObservable to IPublisher conversions.
    /// </summary>
    public enum BackpressureStrategy
    {
        Error,
        Drop,
        Buffer,
        Latest
    }
}
