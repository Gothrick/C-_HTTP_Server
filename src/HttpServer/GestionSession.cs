using System.Collections.Concurrent;

namespace SimpleHttpServer
{
    /// <summary>
    /// Garde les sessions en mémoire, retrouvées grâce au cookie « sessionId ».
    /// </summary>
    internal class GestionSession
    {
        private static readonly ConcurrentDictionary<string, Session> sessions = new ConcurrentDictionary<string, Session>();

        /// <summary>
        /// Retrouve la session du cookie de la requête, ou en crée une nouvelle
        /// (son identifiant est alors placé dans requete.newIdsession pour être envoyé au client).
        /// </summary>
        public static Session ObtenirOuCreerSession(HttpRequete requete)
        {
            string? sessionId = requete.ObtenirCookie("sessionId");

            if (sessionId != null && sessions.TryGetValue(sessionId, out Session? existante))
            {
                return existante;
            }

            string newSessionId = Guid.NewGuid().ToString();
            var session = new Session(newSessionId);
            sessions[newSessionId] = session;
            requete.newIdsession = newSessionId;
            return session;
        }
    }
}
