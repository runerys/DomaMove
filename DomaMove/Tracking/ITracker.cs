using System;

namespace DomaMove.Tracking
{
    public interface ITracker
    {
        void Startup();
        void TransferCompleted(int successfulCount, int failedCount);
        void TransferException(Exception e);
        void Shutdown();
        void UnhandledException(Exception e);
    }
}