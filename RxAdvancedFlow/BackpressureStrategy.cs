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
        /// <summary>
        /// Signals an OnError with an MissingBackpressureException if the downstream can't keep up.
        /// </summary>
        Error,
        /// <summary>
        /// Drops OnNext signals (oldest or latest) if the downstream can't keep up.
        /// </summary>
        Drop,
        /// <summary>
        /// Buffers OnNext signals (bounded or unbounded) if the downstream can't keep up.
        /// </summary>
        Buffer,
        /// <summary>
        /// Keeps only the latest OnNext signal (evicting an unclaimed previous signal) if the downstream can't keep up.
        /// </summary>
        Latest
    }
}
