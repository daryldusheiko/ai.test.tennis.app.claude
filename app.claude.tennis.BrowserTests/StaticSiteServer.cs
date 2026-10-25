using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SocialTennis.BrowserTests;

/// <summary>
/// Serves a published site the way GitHub Pages does: files from a path, and 404.html (with a 404 status) for anything else.
/// It can be stopped and restarted on the same port so tests can really take the server away.
/// </summary>
internal sealed class StaticSiteServer(string directory, string pathBase) : IAsyncDisposable
{
    private WebApplication? _app;

    public int Port { get; } = FindFreePort();

    public Uri BaseAddress => new($"http://localhost:{Port}{pathBase}");

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{Port}");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        var files = new PhysicalFileProvider(directory);
        app.UsePathBase(pathBase.TrimEnd('/'));
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ServeUnknownFileTypes = true,
            ContentTypeProvider = new FileExtensionContentTypeProvider(),
        });
        app.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(files.GetFileInfo("404.html"));
        });

        await app.StartAsync();
        _app = app;
    }

    public async Task StopAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
