using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;


namespace OverZeroTypes.Synchronization
{
    public class QueueToResource<T> : IDisposable where T : class
    {
        private readonly T _resource;
        private T _capturedResource;
#if DEBUG
        long _lastFreeId;
#endif
       ConcurrentQueue<AcquirerInfo<T>> _acquirers;

        public QueueToResource(T resource)
        {
            _acquirers = new ConcurrentQueue<AcquirerInfo<T>>();
            _resource = resource;
        }

        public Task<T> AcquireResource(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
#if DEBUG
            var newId = Interlocked.Increment(ref _lastFreeId);
#endif
            var aInfo = new AcquirerInfo<T>(cancellationToken
#if DEBUG 
            , newId
#endif
                );
            _acquirers.Enqueue(aInfo);

            ConveyResource();

            return aInfo.Task;
        }

        public void ReleaseResource()
        {
            if (_disposedValue)
                return;

            Interlocked.CompareExchange(ref _capturedResource, _resource, null);

            ConveyResource();
        }

        private bool ConveyResource()
        {
            bool isResPassed = false;
            while (_acquirers.TryPeek(out AcquirerInfo<T> itemPeeked))
            {
                T acquireResource = Interlocked.CompareExchange(ref _capturedResource, null, _resource);
                if (acquireResource != null)
                {
                    if (_acquirers.TryDequeue(out AcquirerInfo<T> itemDequeued))
                    {
                        if (!itemDequeued.Task.IsCompleted && itemDequeued.PassAcquiredResource(acquireResource))
                        {
                            isResPassed = true;
                            break;
                        }
                        else
                        {
                            Interlocked.CompareExchange(ref _capturedResource, _resource, null);
                        }
                        itemDequeued.Dispose();
                    }
                    else
                    {
                        Interlocked.CompareExchange(ref _capturedResource, _resource, null);
                        break;
                    }
                }
                else
                {
                    break;
                }
            };


            return isResPassed;
        }


        class AcquirerInfo<T2> : IDisposable
        {
            private TaskCompletionSource<T2> _acquirerTask;
            private CancellationToken _cancelToken;
            private CancellationTokenRegistration _ctRegistration;

            public AcquirerInfo(CancellationToken cancellationToken
#if DEBUG
                ,long id
#endif
                )
            {
#if DEBUG
                Id = id;
#endif
                _acquirerTask = new TaskCompletionSource<T2>();
                _cancelToken = cancellationToken;
                if (cancellationToken != default)
                {
                    _ctRegistration = _cancelToken.Register(TaskCanceledByCancellationToken);
                }
            }

#if DEBUG
            public long Id { get; }
#endif
            public Task<T2> Task => _acquirerTask.Task;

            public bool PassAcquiredResource(T2 resource)
            {
                return _acquirerTask.TrySetResult(resource);
            }

            private void TaskCanceledByCancellationToken()
            {
                _acquirerTask.TrySetCanceled(_cancelToken);
            }

            #region IDisposable Support
            private bool _disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    if (disposing)
                    {
                        _ctRegistration.Dispose();
                        if (!_acquirerTask.Task.IsCompleted)
                        {
                            try
                            {
                                _acquirerTask.SetCanceled();
                            }
                            catch (InvalidOperationException)
                            {
                            }
                        }
                    }

                    _disposedValue = true;
                }
            }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
            }
            #endregion
        }

        #region IDisposable Support
        private volatile bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    while (_acquirers.TryDequeue(out AcquirerInfo<T> aInfo))
                    {
                        aInfo.Dispose();
                    }
                }

                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

        private void CheckDisposed()
        {
            if (_disposedValue)
                throw new ObjectDisposedException("QueueToResource<T>");
        }
    }
}
