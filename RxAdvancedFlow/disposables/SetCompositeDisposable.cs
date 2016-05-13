using System;
using System.Collections.Generic;
using System.Threading;

namespace RxAdvancedFlow.disposables
{
    /// <summary>
    /// A Set-based composite disposable that allows tracking and disposing other
    /// disposables.
    /// </summary>
    public sealed class SetCompositeDisposable : ICompositeDisposable
    {
        HashSet<IDisposable> set;

        bool disposed;

        public SetCompositeDisposable()
        {
        }

        public SetCompositeDisposable(params IDisposable[] disposables)
        {
            set = new HashSet<IDisposable>();
            foreach (IDisposable d in disposables)
            {
                set.Add(d);
            }
        }

        public SetCompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            set = new HashSet<IDisposable>();
            foreach (IDisposable d in disposables)
            {
                set.Add(d);
            }
        }

        public bool Add(IDisposable d)
        {
            if (!IsDisposed())
            {
                lock (this)
                {
                    if (!IsDisposed())
                    {
                        HashSet<IDisposable> _set = set;
                        if (_set == null)
                        {
                            _set = new HashSet<IDisposable>();
                            set = _set;
                        }
                        _set.Add(d);
                        return true;
                    }
                }
            }

            d?.Dispose();
            return false;
        }

        public bool Remove(IDisposable d)
        {
            if (Delete(d))
            {
                d.Dispose();
                return true;
            }
            return false;
        }

        public bool Delete(IDisposable d)
        {
            if (!IsDisposed())
            {
                lock (this)
                {
                    if (!IsDisposed())
                    {
                        HashSet<IDisposable> _set = set;

                        if (_set != null)
                        {
                            return _set.Remove(d);
                        }
                    }
                }
            }
            return false;
        }

        public void Clear()
        {
            if (!IsDisposed())
            {
                HashSet<IDisposable> _set;
                lock (this)
                {
                    if (IsDisposed())
                    {
                        return;
                    }
                    _set = set;
                    set = null;
                }

                if (_set != null)
                {
                    foreach (IDisposable d in _set)
                    {
                        d.Dispose();
                    }
                }
            }

        }

        public void Dispose()
        {
            if (!IsDisposed())
            {
                HashSet<IDisposable> _set;
                lock (this)
                {
                    if (IsDisposed())
                    {
                        return;
                    }
                    _set = set;
                    set = null;
                    Volatile.Write(ref disposed, true);
                }

                if (_set != null)
                {
                    foreach (IDisposable d in _set)
                    {
                        d.Dispose();
                    }
                }
            }
        }

        public bool IsDisposed()
        {
            return Volatile.Read(ref disposed);
        }

        public bool IsEmpty()
        {
            if (IsDisposed())
            {
                return true;
            }

            lock (this)
            {
                HashSet<IDisposable> _set = set;
                return _set == null || _set.Count == 0;
            }
        }
    }
}
