using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
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

        public void TransferException(IEnumerable<Exception> exceptions)
        {
            var enumerable = exceptions as IList<Exception> ?? exceptions.ToList();

            foreach (var exception in enumerable)
            {
                TrackEvent("Exception", "Transfer", exception.Message, 1);
            }

            try
            {
                var sb = new StringBuilder();

                foreach (var exception in enumerable)
                {
                    sb.AppendLine(string.Format("=== {0} ===", DateTime.UtcNow));
                    sb.Append(exception);
                }

                var domaPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                            "DomaMove");

                if(!Directory.Exists(domaPath))
                    Directory.CreateDirectory(domaPath);

                var file = Path.Combine(domaPath, _session.GenerateSessionId() + ".txt");

                File.WriteAllText(file, sb.ToString());

                if (
                    MessageBox.Show("Errors occured. Details logged to: " + file + " Open log file?", "Error logged",
                                    MessageBoxButton.YesNo) ==
                    MessageBoxResult.Yes)
                {
                    Process.Start(file);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }           
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
            using (var tracker = new GoogleAnalyticsTracker.Tracker("UA-40669331-1", "DomaMove", _session))
            {
                tracker.TrackEventAsync(category, action, label, value);
            }
        }
    }
}
