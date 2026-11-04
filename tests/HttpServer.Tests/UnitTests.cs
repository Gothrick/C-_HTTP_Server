using System.IO.Compression;
using System.Text;
using SimpleHttpServer;

namespace HttpServer.Tests;

public class HttpRequeteTests
{
    private static MemoryStream Flux(string texte) => new(Encoding.ASCII.GetBytes(texte));

    [Fact]
    public void Parse_LitLaLigneDeRequeteEtLesEntetes()
    {
        var requete = HttpRequete.Parse(Flux("GET /page.html HTTP/1.1\r\nHost: localhost\r\nAccept-Encoding: gzip\r\n\r\n"));

        Assert.NotNull(requete);
        Assert.Equal("GET", requete.method);
        Assert.Equal("/page.html", requete.url);
        Assert.Equal("HTTP/1.1", requete.protocol);
        Assert.Equal("localhost", requete.headers["Host"]);
    }

    [Fact]
    public void Parse_LesNomsDEntetesIgnorentLaCasse()
    {
        var requete = HttpRequete.Parse(Flux("GET / HTTP/1.1\r\naccept-encoding: br\r\n\r\n"));

        Assert.Equal("br", requete!.headers["Accept-Encoding"]);
    }

    [Fact]
    public void Parse_LitLeCorpsSelonContentLength()
    {
        var requete = HttpRequete.Parse(Flux("POST /formulaire HTTP/1.1\r\nContent-Length: 8\r\n\r\nnom=Anna"));

        Assert.Equal("nom=Anna", requete!.body);
    }

    [Fact]
    public void Parse_RetourneNullPourUneConnexionVide()
    {
        Assert.Null(HttpRequete.Parse(Flux("")));
    }

    [Fact]
    public void Parse_RefuseUneLigneDeRequeteMalFormee()
    {
        Assert.Throws<Exception>(() => HttpRequete.Parse(Flux("GET\r\n\r\n")));
    }

    [Fact]
    public void ObtenirCookie_RetrouveLaValeurParmiPlusieursCookies()
    {
        var requete = HttpRequete.Parse(Flux("GET / HTTP/1.1\r\nCookie: a=1; sessionId=abc-123; b=2\r\n\r\n"))!;

        Assert.Equal("abc-123", requete.ObtenirCookie("sessionId"));
        Assert.Null(requete.ObtenirCookie("absent"));
    }

    [Fact]
    public void ObtenirCookie_RetourneNullSansEnteteCookie()
    {
        var requete = HttpRequete.Parse(Flux("GET / HTTP/1.1\r\n\r\n"))!;

        Assert.Null(requete.ObtenirCookie("sessionId"));
    }
}

public class CompresseurTests
{
    private static readonly byte[] Donnees = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Bonjour le réseau ! ", 50)));

    [Fact]
    public void Gzip_PeutEtreDecompresse()
    {
        byte[] compresse = Compresseur.CompressGzip(Donnees);

        using var sortie = new MemoryStream();
        using (var gzip = new GZipStream(new MemoryStream(compresse), CompressionMode.Decompress))
            gzip.CopyTo(sortie);

        Assert.True(compresse.Length < Donnees.Length);
        Assert.Equal(Donnees, sortie.ToArray());
    }

    [Fact]
    public void Brotli_PeutEtreDecompresse()
    {
        byte[] compresse = Compresseur.CompressBrotli(Donnees);

        using var sortie = new MemoryStream();
        using (var brotli = new BrotliStream(new MemoryStream(compresse), CompressionMode.Decompress))
            brotli.CopyTo(sortie);

        Assert.Equal(Donnees, sortie.ToArray());
    }
}

public class FormulaireTests
{
    [Fact]
    public void ParseFormData_DecodeLesPlusEtLesPourcentages()
    {
        var champs = SimpleHttpServer.HttpServer.ParseFormData("nom=Anne+Marie&ville=Saint%2DJean");

        Assert.Equal("Anne Marie", champs["nom"]);
        Assert.Equal("Saint-Jean", champs["ville"]);
    }

    [Fact]
    public void ParseFormData_IgnoreLesPairesInvalides()
    {
        var champs = SimpleHttpServer.HttpServer.ParseFormData("sansvaleur&nom=Luc");

        Assert.Single(champs);
        Assert.Equal("Luc", champs["nom"]);
    }
}
