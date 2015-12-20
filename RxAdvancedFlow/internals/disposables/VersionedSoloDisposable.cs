using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.disposables
{
    /// <summary>
    /// A solo disposable container which associates a version number with its
    /// contained disposable and ensures only a higher versioned disposable
    /// can replace it.
    /// </summary>
    sealed class VersionedSoloDisposable : IDisposable
    {
        sealed class Node
        {
            internal readonly long version;
            internal readonly IDisposable disposable;

            public Node(long version, IDisposable disposable)
            {
                this.version = version;
                this.disposable = disposable;
            }
        }

        Node node;

        /// <summary>
        /// The first node in any instance of the VersionedSoloDisposable class.
        /// </summary>
        static readonly Node First = new Node(long.MinValue, EmptyDisposable.Instance);
        /// <summary>
        /// This node represents the disposed state of this VersionedSoloDisposable class.
        /// </summary>
        static readonly Node Disposed = new Node(long.MaxValue, DisposableHelper.Disposed);

        public VersionedSoloDisposable()
        {
            Volatile.Write(ref node, First);
        }

        public bool Set(long version, IDisposable disposable)
        {
            Node next = null;
            for (;;)
            {
                Node a = Volatile.Read(ref node);
                if (a == Disposed)
                {
                    disposable?.Dispose();
                    return false;
                }

                if (a.version >= version)
                {
                    return true;
                }

                if (next == null)
                {
                    next = new Node(version, disposable);
                }

                if (Interlocked.CompareExchange(ref node, next, a) == a)
                {
                    return true;
                }
            }
        }
        
        public void Dispose()
        {
            Node a = Volatile.Read(ref node);
            if (a != Disposed)
            {
                a = Interlocked.Exchange(ref node, Disposed);
                if (a != Disposed)
                {
                    a.disposable?.Dispose();
                }
            }
        }

        public bool IsDisposed()
        {
            return Volatile.Read(ref node) == Disposed;
        }
    }
}
