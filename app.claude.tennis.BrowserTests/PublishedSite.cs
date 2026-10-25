using System.Diagnostics;

namespace SocialTennis.BrowserTests;

/// <summary>Publishes the Release build once, using the same scripts as the deploy, and serves it from two addresses.</summary>
internal static class PublishedSite
{
    public const string SubPath = "/tennis/";

    private static string? _workDirectory;

    public static StaticSiteServer Root { get; private set; } = null!;

    public static StaticSiteServer Sub { get; private set; } = null!;

    public static async Task PublishAsync()
    {
        var repo = FindRepositoryRoot();
        _workDirectory = Path.Combine(Path.GetTempPath(), "social-tennis-browser-tests-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(_workDirectory, "root");
        await RunAsync(repo, "scripts/publish.sh", output, "/");

        var sub = Path.Combine(_workDirectory, "sub");
        CopyDirectory(Path.Combine(output, "site"), sub);
        await RunAsync(repo, "scripts/set-base.sh", sub, SubPath);

        Root = new StaticSiteServer(Path.Combine(output, "site"), "/");
        Sub = new StaticSiteServer(sub, SubPath);
        await Root.StartAsync();
        await Sub.StartAsync();
    }

    public static async Task CleanUpAsync()
    {
        await Root.DisposeAsync();
        await Sub.DisposeAsync();
        if (_workDirectory is not null)
        {
            Directory.Delete(_workDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SocialTennis.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("SocialTennis.slnx was not found above " + AppContext.BaseDirectory);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static async Task RunAsync(string repo, string script, params string[] arguments)
    {
        var start = new ProcessStartInfo("bash")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(script);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + script);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{script} failed ({process.ExitCode}):\n{await output}\n{await error}");
        }
    }
}
