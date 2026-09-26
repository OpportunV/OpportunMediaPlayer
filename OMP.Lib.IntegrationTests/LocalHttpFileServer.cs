using System.Net;
using System.Net.Sockets;

namespace OMP.Lib.IntegrationTests;

/// <summary>
/// Serves one file over plain HTTP on loopback, with byte-range support - FFmpeg's http protocol
/// seeks via Range requests (an mp4's index can sit at the end of the file), so a server that
/// ignored them would exercise a different, non-seekable code path than a real CDN does.
/// </summary>
internal sealed class LocalHttpFileServer : IDisposable
{
    public string Url { get; }

    private readonly HttpListener _listener = new();
    private readonly byte[] _content;
    private readonly Task _serveLoop;

    public LocalHttpFileServer(string filePath)
    {
        _content = File.ReadAllBytes(filePath);

        var port = FindFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        Url = prefix + Path.GetFileName(filePath);
        _serveLoop = Task.Run(ServeLoop);
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();

        try
        {
            _serveLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
    }

    private static int FindFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task ServeLoop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => Serve(context));
        }
    }

    private void Serve(HttpListenerContext context)
    {
        var response = context.Response;

        try
        {
            long start = 0;
            long end = _content.Length - 1;

            if (context.Request.Headers["Range"] is { } range && range.StartsWith("bytes=", StringComparison.Ordinal))
            {
                var bounds = range["bytes=".Length..].Split('-');
                start = long.Parse(bounds[0]);
                if (bounds.Length > 1 && bounds[1].Length > 0)
                {
                    end = Math.Min(end, long.Parse(bounds[1]));
                }

                response.StatusCode = (int)HttpStatusCode.PartialContent;
                response.AddHeader("Content-Range", $"bytes {start}-{end}/{_content.Length}");
            }

            response.AddHeader("Accept-Ranges", "bytes");
            response.ContentType = "application/octet-stream";
            response.ContentLength64 = end - start + 1;
            response.OutputStream.Write(_content, (int)start, (int)(end - start + 1));
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
            // FFmpeg routinely drops a connection mid-body when it seeks elsewhere.
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
            }
        }
    }
}
