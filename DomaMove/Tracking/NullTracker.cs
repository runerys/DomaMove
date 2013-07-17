using System;
using System.Collections.Generic;

namespace DomaMove.Tracking
{
    public class NullTracker : ITracker
    {
        public void Startup()
        {
            
        }

        public void TransferCompleted(int successfulCount, int failedCount)
        {
        }

        public void TransferException(IEnumerable<Exception> exceptions)
        {
        }

        public void Shutdown()
        {
        }

        public void UnhandledException(Exception e)
        {
            
        }
    }
}