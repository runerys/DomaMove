namespace DomaMove.Engine
{
    public enum Role
    {
        Source,
        Target
    }

    public class ConnectionSettings
    {
        public string Url { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public Role Role { get; private set; }

        public ConnectionSettings(Role role)
        {
            Role = role;
        }
    }
}