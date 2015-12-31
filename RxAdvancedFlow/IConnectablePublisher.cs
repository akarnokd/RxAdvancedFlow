using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    /// <summary>
    /// An IPublisher with the ability to connect to an actual source IPublisher.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IConnectablePublisher<T> : IPublisher<T>
    {
        void Connect(Action<IDisposable> onConnect);
    }
}
