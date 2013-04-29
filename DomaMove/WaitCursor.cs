using System;
using System.Windows.Input;

namespace DomaMove
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
            Mouse.OverrideCursor = null;
        }
    }
}