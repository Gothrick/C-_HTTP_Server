using System.Text;

namespace SimpleHttpServer
{
    /// <summary>
    /// Permet de parser les requêtes entrantes : ligne de requête, en-têtes et corps.
    /// </summary>
    internal class HttpRequete
    {
        public string method { get; private set; } = "";
        public string url { get; private set; } = "";
        public string protocol { get; private set; } = "";

        // Les noms d'en-têtes HTTP ne sont pas sensibles à la casse.
        public Dictionary<string, string> headers { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public string body { get; private set; } = "";

        // Renseigné quand une nouvelle session vient d'être créée pour cette requête.
        public string? newIdsession { get; set; } = null;

        /// <summary>
        /// Lit une requête sur le flux. Retourne null si la connexion est vide.
        /// </summary>
        /// <param name="stream">Le flux de la connexion.</param>
        /// <exception cref="Exception">Si la ligne de requête est mal formée.</exception>
        public static HttpRequete? Parse(Stream stream)
        {
            StreamReader reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            string? ligne = reader.ReadLine();
            if (string.IsNullOrEmpty(ligne)) return null;

            var parts = ligne.Split(' ');
            if (parts.Length < 3) throw new Exception("Requête mal formée");

            HttpRequete req = new HttpRequete
            {
                method = parts[0],
                url = parts[1],
                protocol = parts[2]
            };

            string? line;
            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                int sep = line.IndexOf(':');
                if (sep == -1) continue;
                string nom = line.Substring(0, sep).Trim();
                string val = line.Substring(sep + 1).Trim();
                req.headers[nom] = val;
            }

            if (req.method == "POST" && req.headers.TryGetValue("Content-Length", out string? lenStr) && int.TryParse(lenStr, out int length) && length > 0)
            {
                // Read peut rendre moins de caractères que demandé : on boucle jusqu'au bout.
                char[] buffer = new char[length];
                int lu = 0;
                while (lu < length)
                {
                    int n = reader.Read(buffer, lu, length - lu);
                    if (n == 0) break;
                    lu += n;
                }
                req.body = new string(buffer, 0, lu);
            }

            Console.WriteLine("\nRequête reçue et parsée avec succès.");

            return req;
        }

        /// <summary>
        /// Retourne la valeur d'un cookie de la requête, ou null s'il est absent.
        /// </summary>
        /// <param name="nom">Le nom du cookie.</param>
        public string? ObtenirCookie(string nom)
        {
            if (!headers.TryGetValue("Cookie", out string? cookies)) return null;

            foreach (var cookie in cookies.Split(';'))
            {
                var trimme = cookie.Trim();
                if (trimme.StartsWith(nom + "="))
                {
                    return trimme.Substring(nom.Length + 1);
                }
            }

            return null;
        }
    }
}
