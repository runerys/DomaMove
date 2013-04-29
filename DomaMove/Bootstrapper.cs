using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Caliburn.Micro;

namespace DomaMove
{
    public class Bootstrapper : BootstrapperBase
    {
        public Bootstrapper()
            : base(true)
        {
            Start();
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);

            string targetUrl = string.Empty;
            string targetUser = string.Empty;
            string targetPassword = string.Empty;

            string sourceUrl = string.Empty;
            string sourceUser = string.Empty;
            string sourcePassword = string.Empty;

            if (e.Args.Length > 0)
            {
                targetUrl = e.Args[0];

                if (e.Args.Length > 2)
                {
                    targetUser = e.Args[1];
                    targetPassword = e.Args[2];
                }

                if (e.Args.Length == 6)
                {
                    sourceUrl = e.Args[3];
                    sourceUser = e.Args[4];
                    sourcePassword = e.Args[5];
                }
            }

            var source = new DomaConnection { Url = sourceUrl, Username = sourceUser, Password = sourcePassword };           
            var target = new DomaConnection { Url = targetUrl, Username = targetUser, Password = targetPassword };

            var transferViewModel = new MoveViewModel(source, target);
            var windowManager = (WindowManager)GetInstance(typeof(WindowManager), null);

            windowManager.ShowWindow(transferViewModel);
        }

        protected override void Configure()
        {
            base.Configure();
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        }     
    }
}
