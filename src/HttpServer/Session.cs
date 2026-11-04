namespace SimpleHttpServer
{
    /// <summary>
    /// Les données gardées côté serveur pour un visiteur (identifié par un cookie).
    /// </summary>
    internal class Session
    {
        public string Id { get; }
        public Dictionary<string, string> data { get; set; }

        public Session(string id)
        {
            Id = id;
            data = new Dictionary<string, string>();
        }
    }
}
