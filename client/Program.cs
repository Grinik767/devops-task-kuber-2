var host = Environment.GetEnvironmentVariable("WEB_SERVICE_HOST") ?? "localhost";
var portText = Environment.GetEnvironmentVariable("WEB_SERVICE_PORT") ?? "8080";
if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
    throw new ArgumentException("WEB_SERVICE_PORT must be between 1 and 65535");
var address = new UriBuilder("http", host, port, "/").Uri;

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
using var sigterm = System.Runtime.InteropServices.PosixSignalRegistration.Create(
    System.Runtime.InteropServices.PosixSignal.SIGTERM,
    context => { context.Cancel = true; stopping.Cancel(); });

using var handler = new SocketsHttpHandler();
using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) };
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
Console.WriteLine($"Client started: GET {address} every 2 seconds");

try
{
    // First request immediately; subsequent requests follow the timer.
    do
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Version = System.Net.HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            // Service balances connections; close each one to demonstrate different Pods.
            request.Headers.ConnectionClose = true;
            using var response = await http.SendAsync(request, stopping.Token);
            var body = await response.Content.ReadAsStringAsync(stopping.Token);
            Console.WriteLine($"{DateTimeOffset.UtcNow:O} GET {address} -> {(int)response.StatusCode} pod={body.Trim()}");
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            break;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            Console.WriteLine($"{DateTimeOffset.UtcNow:O} GET {address} -> ERROR {e.Message}");
        }
    } while (await timer.WaitForNextTickAsync(stopping.Token));
}
catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
