using System;

namespace RxAdvancedFlow
{
    /// <summary>
    /// An ISingle represents a provider of exactly one element or exactly one Exception to its
    /// ISingleSubscribers.
    /// </summary>
    /// <remarks>
    /// An ISingle can serve multiple ISingleSubscribers subscribed via Subscribe(ISingleSubscriber) dynamically
    /// at various points in time.
    /// 
    /// An ISingle signalling events to its ISingleSubscribers has to and will follow the following protocol:
    /// OnSubscribe (OnSuccess | OnError)?
    /// </remarks>
    /// <typeparam name="T">The type of the element signalled to the ISingleSubscriber</typeparam>
    public interface ISingle<out T>
    {
        /// <summary>
        /// Request the ISingle to signal an element or Exception.
        /// </summary>
        /// <param name="s">The ISingleSubscriber instance that will receive the element or Exception.</param>
        void Subscribe(ISingleSubscriber<T> s);
    }

    /// <summary>
    /// An ISingleSubscriber represents the consumer of exactly one element or exactly one Exception signalled
    /// from an ISingle provider.
    /// </summary>
    /// <remarks>
    /// Subscribing an ISingleSubscriber to multiple ISingle providers are generally discouraged and
    /// requires the implementor of ISingleSubscriber to handle merging signals from multiple sources
    /// in a thread-safe manner.
    /// </remarks>
    /// <typeparam name="T">The type of the element received from an ISingle.</typeparam>
    public interface ISingleSubscriber<in T>
    {
        /// <summary>
        /// Invoked by the ISingle after the ISingleSubscriber has been subscribed 
        /// to it via ISingle.Subscribe() method and receives an IDisposable instance 
        /// that can be used for cancelling the subscription.
        /// </summary>
        /// <param name="d">The IDisposable instance used for cancelling the subscription to the ISingle.</param>
        void OnSubscribe(IDisposable d);

        /// <summary>
        /// Invoked by the ISingle when it produced the exactly one element.
        /// </summary>
        /// <remarks>
        /// The call to this method is mutually exclusive with OnError.
        /// </remarks>
        /// <param name="t">The element produced by the source ISingle.</param>
        void OnSuccess(T t);

        /// <summary>
        /// Invoked by the ISingle when it produced the exaclty one Exception.
        /// </summary>
        /// <remarks>
        /// The call to this method is mutually exclusive with OnSucccess.
        /// </remarks>
        /// <param name="e">The Exception produced by the source ISingle</param>
        void OnError(Exception e);
    }

    /// <summary>
    /// An ISingleProcessor is a combination of an ISingleSubscriber and an ISingle, adhering
    /// to both contracts at the same time and may represent a single-use processing stage.
    /// </summary>
    /// <typeparam name="T">The type of element signaled to the ISingleSubscriber side.</typeparam>
    /// <typeparam name="R">The type of element signaled by the ISingle side.</typeparam>
    public interface ISingleProcessor<in T, out R> : ISingle<R>, ISingleSubscriber<T>
    {

    }

    /// <summary>
    /// An ICompletable represents the provider of exactly one completion or exactly one error signal
    /// and is mainly useful for composing side-effecting computation that don't generate values.
    /// </summary>
    /// <remarks>
    /// An ICompletable can serve multiple ICompletableSubscriber subscribed via Subscribe(ICompletableSubscriber) dynamically
    /// at various points in time.
    /// 
    /// An ICompletable signalling events to its ICompletableSubscribers has to and will follow the following protocol:
    /// OnSubscribe (OnError | OnComplete)?
    /// </remarks>
    public interface ICompletable
    {
        /// <summary>
        /// Request the ICompletable to signal a completion or an error signal.
        /// </summary>
        /// <param name="s">The ICompletableSubscriber instance that will receive either the completion
        /// signal or the error signal.</param>
        void Subscribe(ICompletableSubscriber s);
    }

    /// <summary>
    /// An ICompletableSubscriber represents the receiver of exaclty one completion or exactly one error signal
    /// from an ICompletable provider.
    /// </summary>
    /// <remarks>
    /// Subscribing an ICompletableSubscriber to multiple ICompletable providers are generally discouraged and
    /// requires the implementor of ICompletableSubscriber to handle merging signals from multiple sources
    /// in a thread-safe manner.
    /// </remarks>
    public interface ICompletableSubscriber
    {
        /// <summary>
        /// Invoked by the ICompletable after the ICompletableSubscriber has been subscribed 
        /// to it via ICompletable.Subscribe() method and receives an IDisposable instance 
        /// that can be used for cancelling the subscription.
        /// </summary>
        /// <param name="d">The IDisposable instance used for cancelling the subscription to the ICompletable.</param>
        void OnSubscribe(IDisposable d);


        /// <summary>
        /// Invoked by the ICompletable when it produced the exaclty one Exception.
        /// </summary>
        /// <remarks>
        /// The call to this method is mutually exclusive with OnComplete.
        /// </remarks>
        /// <param name="e">The Exception produced by the source ICompletable</param>
        void OnError(Exception e);

        /// <summary>
        /// Invoked by the ICompletable when it produced the exactly one completion signal.
        /// </summary>
        /// <remarks>
        /// The call to this method is mutually exclusive with OnError.
        /// </remarks>
        void OnComplete();
    }

    /// <summary>
    /// An ICompletableProcessor is a combination of an ICompletableSubscriber and an ICompletable, adhering
    /// to both contracts at the same time and may represent a single-use processing stage.
    /// </summary>
    public interface ICompletableProcessor : ICompletable, ICompletableSubscriber
    {

    }
}
