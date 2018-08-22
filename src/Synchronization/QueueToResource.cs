using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OverZeroTypes.Synchronization
{
    public class QueueToResource<T> where T : class
    {
        T _resource;
        T _capturedResource;
#if DEBUG
        long _lastFreeId;
#endif
       ConcurrentQueue<T> _acquirers;

        public QueueToResource(T resource)
        {
            _acquirers = new ConcurrentQueue<T>();
        }

        public Task<T> AcquireResource(CancellationToken cancellationToken = default)
        {

        }

        public void ReleaseResource()
        {

        }


        class AcquirerInfo<T>
        {
            TaskCompletionSource<T> _acquirerTask;

            public AcquirerInfo(CancellationToken cancellationToken
                #if DEBUG
                ,long id
                #endif
                )
            {
                _acquirerTask = new TaskCompletionSource<T>();

            }
        }
    }
}
