using System;
using System.Collections.Generic;

namespace DomaMove.Tracking
{
    public interface ITracker
    {
        void Startup();
        void TransferCompleted(int successfulCount, int failedCount);
        void TransferException(IEnumerable<Exception> exceptions);
        void Shutdown();
        void UnhandledException(Exception e);
    }
}