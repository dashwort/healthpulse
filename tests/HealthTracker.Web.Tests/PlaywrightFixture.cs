using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

using Microsoft.Playwright;

namespace HealthTracker.Web.Tests;

public sealed class PlaywrightFixture : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        "HealthPulseTests",
        $"healthpulse-browser-{Guid.NewGuid():N}.db"
    );
    private Process? webProcess;
    private IPlaywright? playwright;

    public string BaseUrl { get; private set; } = string.Empty;

    public IBrowser? Browser { get; private set; }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var port = FindAvailablePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        var webAssembly = Path.Combine(AppContext.BaseDirectory, "HealthTracker.Web.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(webAssembly)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(webAssembly);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseUrl);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings__HealthTracker"] = $"Data Source={databasePath}";
        startInfo.Environment["AccessControl__InitialAdministratorEmail"] =
            "developer@healthpulse.local";
        startInfo.Environment["Authentication__Development__Email"] =
            "developer@healthpulse.local";
        startInfo.Environment["Mobile__Android__LatestVersion"] = "1.2.3";
        startInfo.Environment["Mobile__Android__ApkUrl"] = "https://example.test/healthpulse.apk";
        startInfo.Environment["Mobile__Android__ReleaseNotes"] = "Test release";
        webProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The web test process could not be started.");

        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        await WaitForApplicationAsync(client);

        var glucoseId = HealthTracker.Domain.Models.BuiltInTemplates.All
            .Single(item => item.Code == "glucose")
            .Id;
        using var trackResponse = await client.PostAsync($"/api/templates/{glucoseId}/track", null);
        trackResponse.EnsureSuccessStatusCode();

        playwright = await Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }
        playwright?.Dispose();
        if (webProcess is not null && !webProcess.HasExited)
        {
            webProcess.Kill(entireProcessTree: true);
            await webProcess.WaitForExitAsync();
        }

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private async Task WaitForApplicationAsync(HttpClient client)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(
                    "/.well-known/healthpulse-mobile",
                    cancellation.Token
                );
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The Kestrel process is still starting.
            }

            await Task.Delay(250, cancellation.Token);
        }

        throw new TimeoutException("The web application did not start within 30 seconds.");
    }

    private static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
