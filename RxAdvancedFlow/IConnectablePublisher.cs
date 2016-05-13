using Reactive.Streams;
using System;

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
