using Microsoft.AspNetCore.Connections;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LegendsViewer.Frontend;

public static class WebAppStaticServer
{
    public const uint WebAppPort = 15422;
    public static readonly string WebAppUrl = $"http://localhost:{WebAppPort}";

    // The backend runs out of its own bin directory, but during development
    // the Vite build is generated under the frontend project. A copied
    // index.html without its hashed assets results in a blocked JS module.
    private static string ResolveWebRoot()
    {
        string deployed = Path.Combine(AppContext.BaseDirectory, "legends-viewer-frontend", "dist");
        string? source = FindSourceBuild(Directory.GetCurrentDirectory());
        bool isDevelopment = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development", StringComparison.OrdinalIgnoreCase);

        string[] candidates = isDevelopment
            ? [source ?? string.Empty, deployed]
            : [deployed, source ?? string.Empty];

        List<string> failures = [];
        foreach (string candidate in candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct())
        {
            if (HasMatchingBuildAssets(candidate, out string explanation))
            {
                return candidate;
            }
            failures.Add(explanation);
        }

        throw new InvalidOperationException(
            "The Legends Viewer frontend build is missing or incomplete. " +
            "Run 'npm run build' in LegendsViewer.Frontend/legends-viewer-frontend, " +
            "then rebuild/restart the .NET application. " + string.Join(" ", failures));
    }

    private static string? FindSourceBuild(string startingDirectory)
    {
        DirectoryInfo? directory = new(startingDirectory);
        for (int depth = 0; depth < 8 && directory != null; depth++, directory = directory.Parent)
        {
            string project = Path.Combine(directory.FullName,
                "LegendsViewer.Frontend", "legends-viewer-frontend", "dist");
            if (Directory.Exists(project)) return project;

            string frontend = Path.Combine(directory.FullName, "legends-viewer-frontend", "dist");
            if (Directory.Exists(frontend)) return frontend;
        }
        return null;
    }

    // Vite writes content-hashed script and stylesheet names into dist/index.html.
    // Do not serve an index if any of those files are missing: the browser would
    // otherwise report a misleading module MIME-type error.
    public static bool HasMatchingBuildAssets(string webRoot, out string explanation)
    {
        string indexPath = Path.Combine(webRoot, "index.html");
        if (!File.Exists(indexPath))
        {
            explanation = $"No frontend index.html at {indexPath}.";
            return false;
        }

        string html = File.ReadAllText(indexPath);
        MatchCollection assetTags = Regex.Matches(html,
            @"(?:src|href)\s*=\s*[""'](?<asset>/assets/[^""']+)[""']",
            RegexOptions.IgnoreCase);
        if (assetTags.Count == 0)
        {
            explanation = $"No compiled Vite asset references found in {indexPath}.";
            return false;
        }

        foreach (Match tag in assetTags)
        {
            string asset = tag.Groups["asset"].Value.TrimStart('/');
            string filePath = Path.Combine(webRoot,
                asset.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
            {
                explanation = $"Missing compiled frontend asset {filePath}, referenced by {indexPath}.";
                return false;
            }
        }

        explanation = string.Empty;
        return true;
    }

    public static async Task RunAsync()
    {
        string webRoot;
        try
        {
            webRoot = ResolveWebRoot();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return;
        }

        var options = new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = webRoot
        };
        var builder = WebApplication.CreateBuilder(options);
        var app = builder.Build();

        // Browsers must revalidate index.html after rebuilds. A cached index
        // pointing to a deleted /assets/index-HASH.js produces a MIME error.
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.ContentType?.StartsWith(
                    "text/html", StringComparison.OrdinalIgnoreCase) == true)
                {
                    context.Response.Headers.CacheControl = "no-store, must-revalidate";
                }
                return Task.CompletedTask;
            });
            return next();
        });

        app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = ["index.html"] });
        app.UseStaticFiles();

        // Make missing bundles an explicit 404 rather than a silent SPA fallback.
        app.MapGet("/assets/{**asset}", (string asset) =>
            Results.Text($"Compiled frontend asset not found: /assets/{asset}. Rebuild and restart Legends Viewer.",
                "text/plain", statusCode: StatusCodes.Status404NotFound));
        app.MapFallbackToFile("index.html");

        try
        {
            Console.WriteLine($"Serving Legends Viewer frontend from: {webRoot}");
            await app.RunAsync($"http://*:{WebAppPort}");
        }
        catch (IOException exception) when (exception.InnerException is AddressInUseException)
        {
            Console.Error.WriteLine($"Address already in use: {WebAppUrl}");
            Console.Error.WriteLine(
                "An older Legends Viewer frontend may still be running. Stop that process before restarting.");
        }
    }

    public static async Task OpenPageInBrowserAsync()
    {
        await Task.Delay(200);
        try
        {
            Process.Start(new ProcessStartInfo(WebAppUrl) { UseShellExecute = true })?.WaitForExit();
        }
        catch
        {
            Console.WriteLine($"Failed to open URL: {WebAppUrl}");
        }
    }
}
