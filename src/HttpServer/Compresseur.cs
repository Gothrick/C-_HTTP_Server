using System.IO.Compression;

namespace SimpleHttpServer
{
    /// <summary>
    /// Compresse le corps des réponses (Gzip ou Brotli) selon l'en-tête Accept-Encoding du client.
    /// </summary>
    internal class Compresseur
    {
        /// <summary>
        /// Applique la compression acceptée par le client, si elle existe.
        /// Gzip est préféré à Brotli.
        /// </summary>
        public static void AppliquerCompression(HttpRequete requete, HttpResponse reponse)
        {
            if (!requete.headers.TryGetValue("Accept-Encoding", out string? encoding))
                return;

            Console.WriteLine("\n(Compression) Accept-Encoding reçu : " + encoding);

            if (encoding.Contains("gzip"))
            {
                reponse.Body = CompressGzip(reponse.Body);
                reponse.Headers["Content-Encoding"] = "gzip";
                reponse.Headers["Content-Length"] = reponse.Body.Length.ToString();
                Console.WriteLine("\nCompression GZIP appliquée.");
            }
            else if (encoding.Contains("br"))
            {
                reponse.Body = CompressBrotli(reponse.Body);
                reponse.Headers["Content-Encoding"] = "br";
                reponse.Headers["Content-Length"] = reponse.Body.Length.ToString();
                Console.WriteLine("\nCompression Brotli appliquée.");
            }
        }

        /// <summary>
        /// Compresser avec Gzip.
        /// </summary>
        public static byte[] CompressGzip(byte[] data)
        {
            using MemoryStream compressedStream = new();
            using (var gzip = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return compressedStream.ToArray();
        }

        /// <summary>
        /// Compresser avec Brotli.
        /// </summary>
        public static byte[] CompressBrotli(byte[] data)
        {
            using MemoryStream compressedStream = new();
            using (var brotli = new BrotliStream(compressedStream, CompressionMode.Compress))
            {
                brotli.Write(data, 0, data.Length);
            }
            return compressedStream.ToArray();
        }
    }
}
