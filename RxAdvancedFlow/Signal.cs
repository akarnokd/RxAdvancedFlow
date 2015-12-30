using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RxAdvancedFlow
{
    /// <summary>
    /// Represents the kind of a signal: OnNext, OnError or OnComplete.
    /// </summary>
    public enum SignalKind
    {
        OnNext, OnError, OnComplete
    }

    /// <summary>
    /// Container for various signal kinds and their values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Signal<T>
    {
        public bool IsOnNext()
        {
            return Kind == SignalKind.OnNext;
        }

        public bool IsOnError()
        {
            return Kind == SignalKind.OnError;
        }

        public bool IsOnComplete()
        {
            return Kind == SignalKind.OnComplete;
        }

        public abstract SignalKind Kind { get; }

        public abstract T Value { get; }

        public abstract Exception Error { get; }

        public static Signal<T> CreateOnNext(T value)
        {
            return new SignalNext<T>(value);
        }

        public static Signal<T> CreateOnError(Exception e)
        {
            return new SignalError<T>(e);
        }

        public static Signal<T> CreateOnComplete()
        {
            return OnComplete;
        }

        static readonly Signal<T> OnComplete = new SignalComplete<T>();
    }

    sealed class SignalNext<T> : Signal<T>
    {
        readonly T value;

        public SignalNext(T value)
        {
            this.value = value;
        }

        public override Exception Error
        {
            get
            {
                return null;
            }
        }

        public override SignalKind Kind
        {
            get
            {
                return SignalKind.OnNext;
            }
        }

        public override T Value
        {
            get
            {
                return value;
            }
        }
    }

    sealed class SignalError<T> : Signal<T>
    {
        readonly Exception e;

        public SignalError(Exception e)
        {
            this.e = e;
        }

        public override Exception Error
        {
            get
            {
                return e;
            }
        }

        public override SignalKind Kind
        {
            get
            {
                return SignalKind.OnError;
            }
        }

        public override T Value
        {
            get
            {
                return default(T);
            }
        }
    }

    sealed class SignalComplete<T> : Signal<T>
    {
        public override Exception Error
        {
            get
            {
                return null;
            }
        }

        public override SignalKind Kind
        {
            get
            {
                return SignalKind.OnComplete;
            }
        }

        public override T Value
        {
            get
            {
                return default(T);
            }
        }
    }
}
