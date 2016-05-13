using Reactive.Streams;
using RxAdvancedFlow.internals.queues;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace RxAdvancedFlow.internals.publisher
{
    sealed class PublisherGroupBy<T, K, V> : ISubscriber<T>, ISubscription
    {
        readonly ISubscriber<IGroupedPublisher<K, V>> actual;

        readonly Func<T, K> keyExtractor;

        readonly Func<T, V> valueExtractor;

        readonly int bufferSize;

        readonly ConcurrentDictionary<K, GroupedSubject> map;

        ISubscription s;

        bool done;

        Exception error;

        BasicBackpressureStruct bp;

        SpscLinkedArrayQueueStruct<GroupedSubject> q;

        int wip;

        int once;

        public PublisherGroupBy(ISubscriber<IGroupedPublisher<K, V>> actual,
            Func<T, K> keyExtractor, Func<T, V> valueExtractor, int bufferSize,
            IEqualityComparer<K> keyComparer)
        {
            this.actual = actual;
            this.keyExtractor = keyExtractor;
            this.valueExtractor = valueExtractor;
            this.bufferSize = bufferSize;
            this.map = new ConcurrentDictionary<K, GroupedSubject>(keyComparer);
            this.q.Init(bufferSize);
            this.wip = 1;
        }

        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                bp.Cancel();

                if (Interlocked.Decrement(ref wip) == 0)
                {
                    s.Cancel();
                }
            }
        }

        public void OnComplete()
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                foreach (GroupedSubject gs in map.Values)
                {
                    gs.OnComplete();
                }

                map.Clear();

                Volatile.Write(ref done, true);
                Drain();
            }
        }

        bool IsDone()
        {
            return Volatile.Read(ref done);
        }

        public void OnError(Exception e)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                foreach (GroupedSubject gs in map.Values)
                {
                    gs.OnError(e);
                }

                map.Clear();

                error = e;
                Volatile.Write(ref done, true);
                Drain();
            }
            else
            {
                RxAdvancedFlowPlugins.OnError(e);
            }
        }
        public void OnNext(T t)
        {
            K k;
            V v;

            try
            {
                k = keyExtractor(t);

                v = valueExtractor(t);
            }
            catch (Exception e)
            {
                s.Cancel();

                OnError(e);
                return;
            }

            GroupedSubject gs;

            if (map.TryGetValue(k, out gs))
            {
                gs.OnNext(v);

                s.Request(1);
            }
            else
            if (Volatile.Read(ref once) == 0 && !bp.IsCancelled())
            {
                Interlocked.Increment(ref wip);

                gs = new GroupedSubject(k, this, bufferSize);

                map.TryAdd(k, gs);

                gs.OnNext(v);

                q.Offer(gs);

                Drain();
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (OnSubscribeHelper.SetSubscription(ref this.s, s))
            {
                actual.OnSubscribe(this);
            }
        }

        public void Request(long n)
        {
            if (OnSubscribeHelper.ValidateRequest(n))
            {
                bp.Request(n);
                s.Request(n);
            }
        }

        void Drain()
        {
            if (!bp.Enter())
            {
                return;
            }

            int missed = 1;

            ISubscriber<GroupedSubject> a = actual;

            for (;;)
            {

                if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                long r = bp.Requested();
                long e = 0L;

                while (e != r)
                {
                    bool d = IsDone();

                    GroupedSubject gs;
                    bool empty = !q.Poll(out gs);

                    if (CheckTerminated(d, empty, a))
                    {
                        return;
                    }

                    if (empty)
                    {
                        break;
                    }

                    a.OnNext(gs);

                    e++;
                }

                if (e == r && CheckTerminated(IsDone(), q.IsEmpty(), a))
                {
                    return;
                }

                if (e != 0)
                {
                    if (r != long.MaxValue)
                    {
                        bp.Produced(e);
                    }
                }

                missed = bp.Leave(missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        bool CheckTerminated(bool d, bool empty, ISubscriber<GroupedSubject> a)
        {
            if (bp.IsCancelled())
            {
                return true;
            }
            if (d)
            {
                Exception e = error;
                if (e != null)
                {
                    a.OnError(e);
                    return true;
                }
                else
                if (empty)
                {
                    a.OnComplete();
                    return true;
                }
            }
            return false;
        }

        void InnerCancel(K key)
        {
            GroupedSubject ignored;
            if (map.TryRemove(key, out ignored))
            {
                if (Interlocked.Decrement(ref wip) == 0)
                {
                    s.Cancel();
                }
            }
        }

        void RequestValues(long n)
        {
            s.Request(n);
        }

        public void OnNext(object element)
        {
            throw new NotImplementedException();
        }

        sealed class GroupedSubject : IGroupedPublisher<K, V>, ISubscription
        {
            readonly K key;

            readonly PublisherGroupBy<T, K, V> parent;

            ISubscriber<V> actual;

            int once;

            Exception error;

            bool done;

            BasicBackpressureStruct bp;

            SpscLinkedArrayQueueStruct<V> q;

            public K Key
            {
                get
                {
                    return key;
                }
            }

            public GroupedSubject(K key, PublisherGroupBy<T, K, V> parent, int bufferSize)
            {
                this.key = key;
                this.parent = parent;
                this.q.Init(bufferSize);
            }

            bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal void OnNext(V value)
            {
                if (IsDone() || bp.IsCancelled())
                {
                    return;
                }
            }

            internal void OnError(Exception e)
            {
                if (IsDone() || bp.IsCancelled())
                {
                    return;
                }

                error = e;
                Volatile.Write(ref done, true);
                Drain();
            }

            internal void OnComplete()
            {
                if (IsDone() || bp.IsCancelled())
                {
                    return;
                }

                Volatile.Write(ref done, true);
                Drain();
            }

            void Drain()
            {
                if (!bp.Enter())
                {
                    return;
                }

                int missed = 1;

                ISubscriber<V> a = actual;

                for (;;)
                {
                    if (CheckTerminated(IsDone(), q.IsEmpty(), a))
                    {
                        return;
                    }

                    long r = bp.Requested();
                    long e = 0L;

                    while (e != r)
                    {
                        bool d = IsDone();

                        V v;
                        bool empty = !q.Poll(out v);

                        if (CheckTerminated(d, empty, a))
                        {
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(v);

                        e++;
                    }

                    if (e == r && CheckTerminated(IsDone(), q.IsEmpty(), a))
                    {
                        return;
                    }

                    if (e != 0L)
                    {
                        if (r != long.MaxValue)
                        {
                            bp.Produced(e);
                        }
                    }

                    missed = bp.Leave(missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            bool CheckTerminated(bool d, bool empty, ISubscriber<V> a)
            {
                if (bp.IsCancelled())
                {
                    q.Clear();
                    return true;
                }

                if (d && empty)
                {
                    Exception e = error;
                    if (e != null)
                    {
                        a.OnError(e);
                    }
                    else
                    {
                        a.OnComplete();
                    }
                    return true;
                }
                return false;
            }

            public void Subscribe(ISubscriber<V> s)
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    s.OnSubscribe(this);

                    Volatile.Write(ref actual, s);
                    if (bp.IsCancelled())
                    {
                        Volatile.Write(ref actual, null);
                    } else
                    {
                        Drain();
                    }
                }
                else
                {
                    bool d = Volatile.Read(ref done);
                    if (d)
                    {
                        Exception e = error;
                        if (e != null)
                        {
                            s.OnError(e);
                        }
                        else
                        {
                            s.OnComplete();
                        }
                    }
                    else
                    {
                        s.OnError(new InvalidOperationException("This IGroupedPublisher allows only a single ISubscriber during its lifetime"));
                    }
                }
            }

            public void Request(long n)
            {
                if (OnSubscribeHelper.ValidateRequest(n))
                {
                    bp.Request(n);
                    parent.RequestValues(n);

                    Drain();
                }
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref once, 2, 1) == 1) {

                    Volatile.Write(ref actual, null);

                    bp.Cancel();

                    parent.InnerCancel(key);
                }
            }

            public void Subscribe(ISubscriber subscriber)
            {
                throw new NotImplementedException();
            }
        }
    }
}
