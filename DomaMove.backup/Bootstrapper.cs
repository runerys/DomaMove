using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Caliburn.Micro;
using DomaMove.Engine;
using DomaMove.Tracking;
using DomaMove.UI;
using DomaMove.Wpf;

namespace DomaMove
{
    public sealed class Bootstrapper : BootstrapperBase
    {
        private ConnectionSettingsStorage _settingsStorage = new ConnectionSettingsStorage();
        private ConnectionSettings _sourceSettings, _targetSettings;
        private ITracker _tracker;
        private bool _messageboxOnError;

        public Bootstrapper()
            : base(true)
        {
            StartRuntime();
        }        

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);

            _settingsStorage = new ConnectionSettingsStorage();
            _sourceSettings = _settingsStorage.Load(Role.Source);
            _targetSettings = _settingsStorage.Load(Role.Target);

            var domaClientFactory = new DomaClientFactory();
            var imageDownloader = new ImageDownloader();

            var source = new DomaConnection(domaClientFactory, imageDownloader, _sourceSettings);
            var target = new DomaConnection(domaClientFactory, imageDownloader, _targetSettings);

            _tracker = new Tracker();

            if (ConfigurationManager.AppSettings.AllKeys.Any(x => x == "skiptracking") && ConfigurationManager.AppSettings["skiptracking"] == "true")
                _tracker = new NullTracker();

            if (ConfigurationManager.AppSettings.AllKeys.Any(x => x == "messageboxerrors") &&
                ConfigurationManager.AppSettings["messageboxerrors"] == "true")
                _messageboxOnError = true;

            var transferViewModel = new MoveViewModel(source, target, _tracker);
            var windowManager = (WindowManager)GetInstance(typeof(WindowManager), null);

            windowManager.ShowWindow(transferViewModel);
        }

        protected override void OnExit(object sender, System.EventArgs e)
        {
            if (_settingsStorage != null)
            {
                if (_sourceSettings != null)
                    _settingsStorage.Save(_sourceSettings);

                if (_targetSettings != null)
                    _settingsStorage.Save(_targetSettings);
            }

            base.OnExit(sender, e);
        }

        protected override void Configure()
        {
            base.Configure();
            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
        }

        protected override void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if(_messageboxOnError)
                MessageBox.Show(e.ToString());

            _tracker.UnhandledException(e.Exception);

            e.Handled = true;
        }
    }
}
