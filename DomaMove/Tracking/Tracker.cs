﻿using System;
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

        public void TransferException(string message)
        {
            TrackEvent("Transfer", "Exception", message, 1);
        }

        public void Shutdown()
        {
            TrackEvent("Lifetime", "Shutdown", "Shutdown", 1);
            TrackEvent("Lifetime", "Shutdown", "SessionLengthInSeconds", (int)DateTime.UtcNow.Subtract(_startupTime).TotalSeconds);
        }

        private void TrackEvent(string category, string action, string label, int value)
        {
            using (var tracker = new GoogleAnalyticsTracker.Tracker("UA-40669331-1", "DomaMove.no", _session))
            {
                tracker.TrackEventAsync(category, action, label, value);
            }
        }
    }   

    public interface ITracker
    {
        void Startup();
        void TransferCompleted(int successfulCount, int failedCount);
        void TransferException(string message);
        void Shutdown();
    }

    public class  NullTracker : ITracker
    {
        public void Startup()
        {
            
        }

        public void TransferCompleted(int successfulCount, int failedCount)
        {
        }

        public void TransferException(string message)
        {
        }

        public void Shutdown()
        {
        }
    }
}