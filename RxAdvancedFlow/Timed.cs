using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    /// <summary>
    /// A structure consisting of a value and an associated time.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public struct Timed<T>
    {
        public readonly T value;
        public readonly long time;

        public Timed(T value, long time)
        {
            this.value = value;
            this.time = time;
        }
    }
}
