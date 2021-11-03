using System;
using System.Windows.Input;

namespace DomaMove.Wpf
{
    public class WaitCursor : IDisposable
    {
        public static WaitCursor Start()
        {
            return new WaitCursor();
        }

        private WaitCursor()
        {
            Mouse.OverrideCursor = Cursors.Wait;
        }

        public void Dispose()
        {
           Dispose(true);
        }

        protected virtual void Dispose(bool cleanManagedAndNative)
        {
            if(cleanManagedAndNative)
                Stop();
        }

        public void Stop()
        {
            Mouse.OverrideCursor = null;
        }
    }
}