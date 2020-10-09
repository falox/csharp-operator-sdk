using System;
using System.Threading;

namespace k8s.Operators
{
    /// <summary>
    /// Represents a disposable object
    /// </summary>
    public abstract class Disposable : IDisposable
    {
        private volatile int _barrier;
        private volatile bool _disposing;
        private volatile bool _disposed;

        public bool IsDisposed => _disposed;
        public bool IsDisposing => _disposing;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _barrier, 1, 0) == 0)
            {
                // This block can be executed only once

                _disposing = true;

                if (disposing)
                {
                    DisposeInternal();
                }

                _disposing = false;
                _disposed = true;
            }
        }

        protected virtual void DisposeInternal()
        {
        }
    }
}