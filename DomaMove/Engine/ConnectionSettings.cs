namespace DomaMove.Engine
{
    public enum Role
    {
        Source,
        Target
    }

    public class ConnectionSettings
    {
        private readonly IConnectionSettingsStorage _storage;

        public string Url { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public Role Role { get; private set; }

        public ConnectionSettings(string url, string user, string password, Role role, IConnectionSettingsStorage storage)
        {
            Role = role;
            _storage = storage;

            if (string.IsNullOrEmpty(url))
            {
                Load();
                return;
            }

            Url = url;
            User = user;
            Password = password;
        }

        private void Load()
        {
            _storage.Load(this);
        }

        public void Save()
        {
            _storage.Save(this);
        }
    }

    public interface IConnectionSettingsStorage
    {
        void Load(ConnectionSettings connectionSettings);
        void Save(ConnectionSettings connectionSettings);
    }
}