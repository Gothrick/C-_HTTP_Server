using System.Net;
using System.Net.Sockets;
using SimpleHttpServer;

// Le port se règle avec le premier argument, sinon la variable PORT, sinon 8088.
int port = 8088;
string? valeur = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("PORT");
if (valeur != null && !int.TryParse(valeur, out port))
{
    Console.Error.WriteLine($"Port invalide : {valeur}");
    return 1;
}

TcpListener listener = new TcpListener(IPAddress.Any, port);
HttpServer server = new HttpServer(listener);

Console.WriteLine($"Écoute sur http://localhost:{port}");
server.Start();
return 0;
