using System.Text;

namespace SimpleHttpServer
{
    /// <summary>
    /// Une réponse HTTP : code de statut, en-têtes et corps.
    /// Sait s'écrire sur le flux de la connexion avec les bons en-têtes.
    /// </summary>
    internal class HttpResponse
    {
        public string StatutCode { get; set; } = "200 OK";
        public Dictionary<string, string> Headers { get; private set; } = new();
        public byte[] Body { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Modifier le corps avec du texte (UTF-8).
        /// </summary>
        public void SetBody(string content, string contentType)
        {
            SetBody(Encoding.UTF8.GetBytes(content), contentType);
        }

        /// <summary>
        /// Modifier le corps avec des octets.
        /// </summary>
        public void SetBody(byte[] content, string contentType)
        {
            Body = content;
            Headers["Content-Type"] = contentType;
            Headers["Content-Length"] = Body.Length.ToString();
        }

        /// <summary>
        /// Écrit la réponse sur le flux : ligne de statut, en-têtes, ligne vide, corps.
        /// </summary>
        public void Envoyer(Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true))
            {
                // HTTP exige CRLF, peu importe le système d'exploitation.
                writer.NewLine = "\r\n";

                writer.WriteLine($"HTTP/1.1 {StatutCode}");

                // En-tête personnalisé du laboratoire : défini par la variable
                // d'environnement X_STUDENT_ID, absent si elle n'est pas définie.
                string? idEtudiant = Environment.GetEnvironmentVariable("X_STUDENT_ID");
                if (!string.IsNullOrWhiteSpace(idEtudiant))
                    Headers["X-Etudiant-ID"] = idEtudiant;

                Headers["Server"] = "Serveur";
                Headers["Connection"] = "close";

                foreach (var header in Headers)
                {
                    writer.WriteLine($"{header.Key}: {header.Value}");
                }

                writer.WriteLine(); // Ligne vide avant le corps
                writer.Flush();     // Flush obligatoire pour envoyer les en-têtes avant le corps

                stream.Write(Body, 0, Body.Length);
            }
        }

        /// <summary>
        /// Envoie une page d'erreur simple.
        /// </summary>
        public static void EnvoyerErreur(Stream stream, int code, string message)
        {
            var response = new HttpResponse
            {
                StatutCode = $"{code} {message}"
            };
            string errorHtml = $"<html><body><h1>{code} - {message}</h1></body></html>";
            response.SetBody(errorHtml, "text/html");
            response.Envoyer(stream);
        }
    }
}
