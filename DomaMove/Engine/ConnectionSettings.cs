namespace DomaMove.Engine
{
    public enum Role
    {
        Source,
        Target
    }

    public class ConnectionSettings
    {
        public Role Role { get; private set; }

        public string Url { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        
        public ConnectionSettings(Role role)
        {
            Role = role;
        }

        public string GetHash()
        {
            return string.Format("{0}§§{1}¤¤{2}", Url, User, Password);
        }
    }
}