using System.Diagnostics;
using System.Net.Http;

namespace Imageboard_illust.Web.UiTests;

[SetUpFixture]
public class WebAppFixture
{
    public const string BaseUrl = "http://localhost:5299";

    private static Process? _process;
    private static string? _tempDataDir;

    [OneTimeSetUp]
    public void StartServer()
    {
        _tempDataDir = Path.Combine(Path.GetTempPath(), "ImageboardUiTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDataDir);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --urls {BaseUrl}",
            WorkingDirectory = FindWebProjectDir(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.EnvironmentVariables["IMAGEBOARD_DATA_DIR"] = _tempDataDir;
        psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start web app process.");
        WaitForServerReady();
    }

    [OneTimeTearDown]
    public void StopServer()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        finally
        {
            _process?.Dispose();
            if (_tempDataDir is not null && Directory.Exists(_tempDataDir))
            {
                try { Directory.Delete(_tempDataDir, true); } catch { /* best effort cleanup */ }
            }
        }
    }

    private static string FindWebProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Imageboard_illust.Web")))
            dir = dir.Parent;
        if (dir is null)
            throw new DirectoryNotFoundException("Could not locate the Imageboard_illust.Web project directory.");
        return Path.Combine(dir.FullName, "Imageboard_illust.Web");
    }

    private static void WaitForServerReady()
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = client.GetAsync(BaseUrl + "/").GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode) return;
            }
            catch
            {
                // server not ready yet, keep polling
            }
            Thread.Sleep(500);
        }
        throw new TimeoutException("Web app did not start within the expected time.");
    }
}
