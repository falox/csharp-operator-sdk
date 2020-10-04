using System;

namespace k8s.Operators
{
    /// <summary>
    /// Represents a disposable object
    /// </summary>
    public abstract class Disposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    DisposeInternal();
                }

                IsDisposed = true;
            }
        }

        protected virtual void DisposeInternal()
        {
        }
    }
}