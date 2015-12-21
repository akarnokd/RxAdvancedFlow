using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RxAdvancedFlow.disposables
{
    /// <summary>
    /// A Set-based composite disposable that allows tracking and disposing other
    /// disposables.
    /// </summary>
    public sealed class ListCompositeDisposable : ICompositeDisposable
    {
        LinkedList<IDisposable> set;

        bool disposed;

        public ListCompositeDisposable()
        {
        }

        public ListCompositeDisposable(params IDisposable[] disposables)
        {
            set = new LinkedList<IDisposable>();
            foreach (IDisposable d in disposables)
            {
                set.AddLast(d);
            }
        }

        public ListCompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            set = new LinkedList<IDisposable>();
            foreach (IDisposable d in disposables)
            {
                set.AddLast(d);
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
                        LinkedList<IDisposable> _set = set;
                        if (_set == null)
                        {
                            _set = new LinkedList<IDisposable>();
                            set = _set;
                        }
                        _set.AddLast(d);
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
                        LinkedList<IDisposable> _set = set;

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
                LinkedList<IDisposable> _set;
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
                LinkedList<IDisposable> _set;
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
                LinkedList<IDisposable> _set = set;
                return _set == null || _set.Count == 0;
            }
        }
    }
}
