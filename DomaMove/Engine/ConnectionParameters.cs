namespace DomaMove.Engine
{
    public class ConnectionParameters
    {
        public string Url { get; private set; }
        public string User { get; private set; }
        public string Password { get; private set; }

        public ConnectionParameters(string url, string user, string password)
        {
            Url = url;
            User = user;
            Password = password;
        }
    }
}