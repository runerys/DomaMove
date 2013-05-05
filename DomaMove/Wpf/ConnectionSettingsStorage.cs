using DomaMove.Engine;

namespace DomaMove.Wpf
{
    public class ConnectionSettingsStorage
    {
        public ConnectionSettings Load(Role role)
        {
            var settings = Properties.Settings.Default;

            var connectionSettings = new ConnectionSettings(role);

            if (role == Role.Source)
            {
                connectionSettings.Url = settings.SourceUrl;
                connectionSettings.User = settings.SourceUser;
                connectionSettings.Password = settings.SourcePassword;
            }
            else
            {
                connectionSettings.Url = settings.TargetUrl;
                connectionSettings.User = settings.TargetUser;
                connectionSettings.Password = settings.TargetPassword;
            }

            return connectionSettings;
        }

        public void Save(ConnectionSettings connectionSettings)
        {
            var settings = Properties.Settings.Default;

            if (connectionSettings.Role == Role.Source)
            {
                settings.SourceUrl = connectionSettings.Url;
                settings.SourceUser = connectionSettings.User;
                settings.SourcePassword = connectionSettings.Password;
            }
            else
            {
                settings.TargetUrl = connectionSettings.Url;
                settings.TargetUser = connectionSettings.User;
                settings.TargetPassword = connectionSettings.Password;
            }

            settings.Save();
        }
    }
}
