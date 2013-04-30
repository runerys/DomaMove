using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Caliburn.Micro;
using DomaMove.Engine;
using DomaMove.UI;

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

            var targetUrl = string.Empty;
            var targetUser = string.Empty;
            var targetPassword = string.Empty;
            
            var sourceUrl = string.Empty;
            var sourceUser = string.Empty;
            var sourcePassword = string.Empty;

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

            var sourceParameters = new ConnectionParameters(sourceUrl, sourceUser, sourcePassword);
            var targetParameters = new ConnectionParameters(targetUrl, targetUser, targetPassword);

            var domaClientFactory = new DomaClientFactory();
            var imageDownloader = new ImageDownloader();

            var source = new DomaConnection(domaClientFactory, imageDownloader, sourceParameters);
            var target = new DomaConnection(domaClientFactory, imageDownloader, targetParameters);

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
