using Reactive.Streams;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Represents an IPublisher with an additional key value.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public interface IGroupedPublisher<K, V> : IPublisher<V>
    {
        K Key { get; }
    }
}
