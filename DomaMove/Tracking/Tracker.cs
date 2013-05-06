using System;
using GoogleAnalyticsTracker;

namespace DomaMove.Tracking
{
    public class Tracker : ITracker
    {
        private DateTime _startupTime;
        private readonly IAnalyticsSession _session;

        public Tracker()
        {
            _session = new AnalyticsSession();
        }

        public void Startup()
        {
            _startupTime = DateTime.UtcNow;
            TrackEvent("Lifetime", "Startup", "Startup", 1);
        }        

        public void TransferCompleted(int successfulCount, int failedCount)
        {
            TrackEvent("Transfer", "Completed", "Successful", successfulCount);

            if(failedCount > 0)
                TrackEvent("Transfer", "Completed", "Failed", failedCount);
        }

        public void TransferException(Exception e)
        {
            TrackEvent("Exception", "Transfer", e.Message, 1);
        }

        public void Shutdown()
        {
            TrackEvent("Lifetime", "Shutdown", "Shutdown", 1);
            TrackEvent("Lifetime", "Shutdown", "SessionLengthInSeconds", (int)DateTime.UtcNow.Subtract(_startupTime).TotalSeconds);
        }

        public void UnhandledException(Exception e)
        {
            TrackEvent("Exception", "Unhandled", e.Message, 1);
        }

        private void TrackEvent(string category, string action, string label, int value)
        {
            using (var tracker = new GoogleAnalyticsTracker.Tracker("UA-40669331-1", "DomaMove.no", _session))
            {
                tracker.TrackEventAsync(category, action, label, value);
            }
        }
    }
}
