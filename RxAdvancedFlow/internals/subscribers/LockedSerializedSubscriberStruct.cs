using ReactiveStreamsCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.internals.subscribers
{
    internal struct LockedSerializedSubscriberStruct<T>
    {
        ISubscriber<T> actual;

        Exception error;

        bool cancelled;

        bool done;

        bool emitting;

        bool missed;

        ArrayNode head;

        ArrayNode tail;

        public void Init(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void OnSubscribe(ISubscription s)
        {
            actual.OnSubscribe(s);
        }

        void Add(T value)
        {
            if (tail == null)
            {
                head = new ArrayNode(value);
                tail = head;
            } 
            else
            {
                if (!tail.Add(value))
                {
                    ArrayNode n = new ArrayNode(value);
                    tail.next = n;
                    tail = n;
                }
            }
        }

        internal void Cancel()
        {
            Volatile.Write(ref cancelled, true);
        }

        internal bool IsCancelled()
        {
            return Volatile.Read(ref cancelled);
        }

        internal void OnNext(T value)
        {
            if (IsCancelled())
            {
                return;
            }

            ISubscriber<T> a = actual;

            lock (a)
            {
                if (IsCancelled() || done || error != null)
                {
                    return;
                }

                if (emitting)
                {
                    Add(value);
                    missed = true;
                    return;
                }

                emitting = true;
            }

            a.OnNext(value);

            DrainLoop(a);
        }

        internal void OnError(Exception e)
        {
            if (IsCancelled())
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            bool d = false;

            ISubscriber<T> a = actual;

            lock (a)
            {
                if (IsCancelled() || done || error != null)
                {
                    d = true;
                }
                else
                if (emitting)
                {
                    error = e;
                    missed = true;
                    return;
                }
                else
                {
                    emitting = true;
                    done = true;
                }
            }

            if (d)
            {
                RxAdvancedFlowPlugins.OnError(e);
                return;
            }

            a.OnError(e);
        }

        internal void OnComplete()
        {
            if (IsCancelled())
            {
                return;
            }

            ISubscriber<T> a = actual;

            lock (a)
            {
                if (IsCancelled() || done || error != null)
                {
                    return;
                }

                done = true;
                if (emitting)
                {
                    missed = true;
                    return;
                }

                emitting = true;
            }

            a.OnComplete();
        }

        void DrainLoop(ISubscriber<T> a)
        {
            for (;;)
            {
                ArrayNode n;
                Exception e;
                bool d;

                lock (a)
                {
                    if (!missed)
                    {
                        emitting = false;
                        return;
                    }

                    n = head;
                    e = error;
                    d = done;
                    head = null;
                    tail = null;
                    missed = false;
                }

                if (IsCancelled())
                {
                    return;
                }

                while (n != null)
                {
                    T[] arr = n.array;
                    int len = n.used;
                    for (int i = 0; i < len; i++)
                    {
                        if (IsCancelled())
                        {
                            return;
                        }
                        a.OnNext(arr[i]);
                    }

                    n = n.next;
                }

                if (IsCancelled())
                {
                    return;
                }
                if (e != null)
                {
                    a.OnError(e);
                    return;
                }
                if (d)
                {
                    a.OnComplete();
                    return;
                }
            }
        }

        sealed class ArrayNode
        {
            internal readonly T[] array = new T[16];

            internal int used;

            internal ArrayNode next;

            internal ArrayNode(T value)
            {
                array[0] = value;
                used = 1;
            }

            internal bool Add(T value)
            {
                if (used == array.Length)
                {
                    return false;
                }
                array[used++] = value;
                return true;
            }
        }
    }
}
