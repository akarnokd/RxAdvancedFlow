using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.queues
{
    /// <summary>
    /// A base interface for Queues with standard methods.
    /// </summary>
    /// <typeparam name="T">The value type of the queue.</typeparam>
    public interface IQueue<T>
    {
        bool Offer(T value);

        bool Poll(out T value);

        bool Peek(out T value);

        bool IsEmpty();

        int Size();

        void Clear();
    }
}
