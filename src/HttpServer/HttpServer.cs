using System.Net;
using System.Net.Sockets;

namespace SimpleHttpServer
{
    /// <summary>
    /// Le serveur : accepte les connexions entrantes et traite chaque client dans son propre thread.
    ///
    /// Gère les requêtes GET (fichiers statiques) et POST (formulaire), les sessions par cookie
    /// et la compression des réponses.
    /// </summary>
    internal class HttpServer
    {
        // Pour écouter les connexions
        private readonly TcpListener listener;
        private readonly string racineWeb;
        private volatile bool estEnCours = false;

        /// <param name="listener">Le listener sur lequel écouter.</param>
        /// <param name="racineWeb">
        /// Le dossier des fichiers statiques. Par défaut, le dossier wwwroot
        /// copié à côté de l'exécutable.
        /// </param>
        public HttpServer(TcpListener listener, string? racineWeb = null)
        {
            this.listener = listener;
            this.racineWeb = Path.GetFullPath(racineWeb ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        }

        public void Start()
        {
            listener.Start();
            estEnCours = true;

            Console.WriteLine("Le serveur est en marche !!");

            while (estEnCours)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException) when (!estEnCours)
                {
                    break; // Le serveur a été arrêté
                }

                Console.WriteLine("\nClient connecté !!!");

                // Un thread par client
                Thread clientThread = new Thread(() => GererClient(client));
                clientThread.Start();
            }
        }

        /// <summary>
        /// Arrête le serveur : plus aucune nouvelle connexion n'est acceptée.
        /// </summary>
        public void Stop()
        {
            estEnCours = false;
            listener.Stop();
        }

        /// <summary>
        /// Traite une connexion : lit la requête, prépare la réponse et l'envoie.
        /// </summary>
        public void GererClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                // Un navigateur ouvre parfois une connexion sans rien envoyer : ne pas rester bloqué.
                stream.ReadTimeout = 5000;

                try
                {
                    HttpRequete? requete = HttpRequete.Parse(stream);

                    Console.WriteLine("\n--- Nouvelle requête ---");

                    if (requete == null)
                    {
                        Console.WriteLine("\nConnexion vide ignorée.");
                        return;
                    }

                    Console.WriteLine($"Méthode : {requete.method}");
                    Console.WriteLine($"URL demandée : {requete.url}");
                    Console.WriteLine($"Protocole : {requete.protocol}");

                    Console.WriteLine("En-têtes reçus :");
                    foreach (var h in requete.headers)
                    {
                        Console.WriteLine($"  {h.Key}: {h.Value}");
                    }
                    Console.WriteLine("\n");

                    Session session = GestionSession.ObtenirOuCreerSession(requete);
                    HttpResponse reponse;

                    if (requete.method == "GET")
                    {
                        reponse = ServirFichierStatique(requete.url);
                    }
                    else if (requete.method == "POST" && requete.url == "/formulaire")
                    {
                        reponse = TraiterFormulaire(requete, session);
                    }
                    else
                    {
                        reponse = new HttpResponse();
                        reponse.StatutCode = "400 Bad Request";
                        reponse.SetBody("<h1>Requête invalide</h1>", "text/html");
                    }

                    if (requete.newIdsession != null)
                        reponse.Headers["Set-Cookie"] = $"sessionId={requete.newIdsession}; HttpOnly";

                    Compresseur.AppliquerCompression(requete, reponse);
                    reponse.Envoyer(stream);
                }
                catch (IOException)
                {
                    // Le client est parti ou n'a rien envoyé à temps : rien à répondre.
                    Console.WriteLine("\nConnexion fermée par le client ou délai dépassé.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erreur : " + ex.Message);
                    try
                    {
                        HttpResponse.EnvoyerErreur(stream, 500, "Internal Server Error");
                    }
                    catch (IOException) { }
                }
            }
        }

        /// <summary>
        /// POST /formulaire : garde le nom reçu dans la session et répond avec une page de bienvenue.
        /// </summary>
        private HttpResponse TraiterFormulaire(HttpRequete requete, Session session)
        {
            Console.WriteLine("\nContenu POST : " + requete.body);

            var formData = ParseFormData(requete.body);
            HttpResponse reponse = new HttpResponse();

            if (!formData.TryGetValue("nom", out string? nom) || string.IsNullOrWhiteSpace(nom))
            {
                reponse.StatutCode = "400 Bad Request";
                reponse.SetBody("<html><body><h1>Le champ « nom » est requis</h1></body></html>", "text/html");
                return reponse;
            }

            session.data["nom"] = nom;
            Console.WriteLine($"\nNom enregistré en session : {nom}");

            // Le nom vient du visiteur : on l'encode avant de l'insérer dans la page.
            string html = "<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"UTF-8\"><title>Bienvenue</title>"
                + "<link rel=\"stylesheet\" href=\"/styles.css\"></head><body>"
                + $"<h1>Bienvenue {WebUtility.HtmlEncode(nom)}</h1><p><a href=\"/\">Retour</a></p></body></html>";
            reponse.SetBody(html, "text/html; charset=utf-8");
            return reponse;
        }

        /// <summary>
        /// Découpe un corps « application/x-www-form-urlencoded » en paires clé/valeur.
        /// </summary>
        internal static Dictionary<string, string> ParseFormData(string body)
        {
            var dict = new Dictionary<string, string>();
            foreach (var pair in body.Split('&'))
            {
                var kv = pair.Split('=');
                if (kv.Length == 2)
                {
                    // Dans un formulaire, « + » représente une espace.
                    dict[Decoder(kv[0])] = Decoder(kv[1]);
                }
            }
            return dict;
        }

        private static string Decoder(string valeur) => Uri.UnescapeDataString(valeur.Replace('+', ' '));

        /// <summary>
        /// Lit un fichier du dossier wwwroot et le retourne avec le bon type de contenu.
        /// Refuse tout chemin qui sortirait de ce dossier.
        /// </summary>
        private HttpResponse ServirFichierStatique(string url)
        {
            HttpResponse response = new HttpResponse();

            // On ignore la partie « ?paramètres » et on décode les %xx.
            string chemin = Uri.UnescapeDataString(url.Split('?')[0]);
            if (chemin == "/") chemin = "/index.html";

            string fullPath = Path.GetFullPath(Path.Combine(racineWeb, chemin.TrimStart('/')));

            Console.WriteLine($"\nTentative de lecture du fichier : {fullPath}");

            // Protection contre « /../ » : le fichier doit rester dans wwwroot.
            if (!fullPath.StartsWith(racineWeb + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                Console.WriteLine("\nChemin refusé (hors de wwwroot).");
                response.StatutCode = "403 Forbidden";
                response.SetBody("<html><body><h1>403 - Accès refusé</h1></body></html>", "text/html");
                return response;
            }

            if (!File.Exists(fullPath))
            {
                Console.WriteLine("\nFichier non trouvé.");
                response.StatutCode = "404 Not Found";
                response.SetBody("<html><body><h1>404 - Fichier non trouvé</h1></body></html>", "text/html");
                return response;
            }

            Console.WriteLine("\nFichier trouvé, envoi au client.");

            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            string contentType = extension switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".txt" => "text/plain; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };

            response.SetBody(File.ReadAllBytes(fullPath), contentType);
            return response;
        }
    }
}
