using System;

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

        public void TransferException(Exception e)
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