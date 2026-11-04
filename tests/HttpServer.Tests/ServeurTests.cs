using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SimpleHttpServer;

namespace HttpServer.Tests;

/// <summary>
/// DÃ©marre un vrai serveur sur un port libre, avec un dossier wwwroot temporaire.
/// </summary>
public sealed class ServeurFixture : IDisposable
{
    private readonly SimpleHttpServer.HttpServer serveur;
    private readonly string dossier;

    public int Port { get; }

    public ServeurFixture()
    {
        dossier = Path.Combine(Path.GetTempPath(), "httpserver-tests-" + Guid.NewGuid());
        string wwwroot = Path.Combine(dossier, "wwwroot");
        Directory.CreateDirectory(wwwroot);
        File.WriteAllText(Path.Combine(wwwroot, "index.html"), "<h1>Accueil de test</h1>");
        File.WriteAllText(Path.Combine(wwwroot, "styles.css"), "body{}");
        File.WriteAllText(Path.Combine(dossier, "secret.txt"), "contenu secret");

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;

        serveur = new SimpleHttpServer.HttpServer(listener, wwwroot);
        new Thread(serveur.Start) { IsBackground = true }.Start();
    }

    public void Dispose()
    {
        serveur.Stop();
        Directory.Delete(dossier, recursive: true);
    }

    /// <summary>Envoie une requÃªte brute et lit toute la rÃ©ponse (le serveur ferme la connexion).</summary>
    public Reponse Envoyer(string requeteBrute)
    {
        using var client = new TcpClient("127.0.0.1", Port);
        using var flux = client.GetStream();
        byte[] envoi = Encoding.ASCII.GetBytes(requeteBrute);
        flux.Write(envoi, 0, envoi.Length);

        using var tout = new MemoryStream();
        flux.CopyTo(tout);
        return Reponse.Lire(tout.ToArray());
    }

    public Reponse Get(string url, string entetesSupplementaires = "") =>
        Envoyer($"GET {url} HTTP/1.1\r\nHost: localhost\r\n{entetesSupplementaires}\r\n");

    public Reponse Post(string url, string corps, string entetesSupplementaires = "") =>
        Envoyer($"POST {url} HTTP/1.1\r\nHost: localhost\r\nContent-Length: {corps.Length}\r\n{entetesSupplementaires}\r\n{corps}");
}

public record Reponse(string Statut, Dictionary<string, string> Entetes, byte[] Corps)
{
    public string Texte => Encoding.UTF8.GetString(Corps);

    public static Reponse Lire(byte[] octets)
    {
        string brut = Encoding.Latin1.GetString(octets);
        int fin = brut.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        string[] lignes = brut.Substring(0, fin).Split("\r\n");

        var entetes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string ligne in lignes.Skip(1))
        {
            int sep = ligne.IndexOf(':');
            entetes[ligne.Substring(0, sep)] = ligne.Substring(sep + 1).Trim();
        }

        return new Reponse(lignes[0], entetes, octets.Skip(fin + 4).ToArray());
    }
}

public class ServeurTests : IClassFixture<ServeurFixture>
{
    private readonly ServeurFixture serveur;

    public ServeurTests(ServeurFixture serveur) => this.serveur = serveur;

    [Fact]
    public void Get_Racine_ServeIndexHtml()
    {
        var reponse = serveur.Get("/");

        Assert.Equal("HTTP/1.1 200 OK", reponse.Statut);
        Assert.StartsWith("text/html", reponse.Entetes["Content-Type"]);
        Assert.Contains("Accueil de test", reponse.Texte);
    }

    [Fact]
    public void Get_ChoisitLeTypeDeContenuSelonLExtension()
    {
        var reponse = serveur.Get("/styles.css");

        Assert.Equal("text/css", reponse.Entetes["Content-Type"]);
    }

    [Fact]
    public void Get_IgnoreLesParametresDeRequete()
    {
        var reponse = serveur.Get("/index.html?version=2");

        Assert.Equal("HTTP/1.1 200 OK", reponse.Statut);
    }

    [Fact]
    public void Get_FichierInexistant_Retourne404()
    {
        var reponse = serveur.Get("/absent.html");

        Assert.Equal("HTTP/1.1 404 Not Found", reponse.Statut);
    }

    [Theory]
    [InlineData("/../secret.txt")]
    [InlineData("/%2e%2e/secret.txt")]
    [InlineData("/..%2fsecret.txt")]
    public void Get_SortirDeWwwroot_Retourne403(string url)
    {
        var reponse = serveur.Get(url);

        Assert.Equal("HTTP/1.1 403 Forbidden", reponse.Statut);
        Assert.DoesNotContain("contenu secret", reponse.Texte);
    }

    [Fact]
    public void Reponse_UtiliseCrlfEtFermeLaConnexion()
    {
        var reponse = serveur.Get("/");

        Assert.Equal("close", reponse.Entetes["Connection"]);
        Assert.Equal(reponse.Corps.Length.ToString(), reponse.Entetes["Content-Length"]);
    }

    [Fact]
    public void Get_AvecGzip_CompresseLeCorps()
    {
        var reponse = serveur.Get("/", "Accept-Encoding: gzip, deflate\r\n");

        Assert.Equal("gzip", reponse.Entetes["Content-Encoding"]);
        using var gzip = new GZipStream(new MemoryStream(reponse.Corps), CompressionMode.Decompress);
        using var lecteur = new StreamReader(gzip);
        Assert.Contains("Accueil de test", lecteur.ReadToEnd());
    }

    [Fact]
    public void Get_AvecBrotli_CompresseLeCorps()
    {
        var reponse = serveur.Get("/", "Accept-Encoding: br\r\n");

        Assert.Equal("br", reponse.Entetes["Content-Encoding"]);
        using var brotli = new BrotliStream(new MemoryStream(reponse.Corps), CompressionMode.Decompress);
        using var lecteur = new StreamReader(brotli);
        Assert.Contains("Accueil de test", lecteur.ReadToEnd());
    }

    [Fact]
    public void Get_SansAcceptEncoding_NeCompressePas()
    {
        var reponse = serveur.Get("/");

        Assert.False(reponse.Entetes.ContainsKey("Content-Encoding"));
    }

    [Fact]
    public void Post_Formulaire_AccueilleLeVisiteurEtCreeUneSession()
    {
        var reponse = serveur.Post("/formulaire", "nom=Anne+Marie");

        Assert.Equal("HTTP/1.1 200 OK", reponse.Statut);
        Assert.Contains("Bienvenue Anne Marie", reponse.Texte);
        Assert.StartsWith("sessionId=", reponse.Entetes["Set-Cookie"]);
    }

    [Fact]
    public void Post_Formulaire_ReutiliseLaSessionDuCookie()
    {
        var premiere = serveur.Post("/formulaire", "nom=Luc");
        string cookie = premiere.Entetes["Set-Cookie"].Split(';')[0];

        var seconde = serveur.Post("/formulaire", "nom=Luc", $"Cookie: {cookie}\r\n");

        Assert.False(seconde.Entetes.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public void Post_Formulaire_EncodeLeHtmlDuNom()
    {
        var reponse = serveur.Post("/formulaire", "nom=%3Cscript%3Ealert(1)%3C%2Fscript%3E");

        Assert.DoesNotContain("<script>", reponse.Texte);
        Assert.Contains("&lt;script&gt;", reponse.Texte);
    }

    [Fact]
    public void Post_Formulaire_SansNom_Retourne400()
    {
        var reponse = serveur.Post("/formulaire", "autre=1");

        Assert.Equal("HTTP/1.1 400 Bad Request", reponse.Statut);
    }

    [Fact]
    public void Post_VersUneAutreUrl_Retourne400()
    {
        var reponse = serveur.Post("/inconnu", "nom=Luc");

        Assert.Equal("HTTP/1.1 400 Bad Request", reponse.Statut);
    }
}

