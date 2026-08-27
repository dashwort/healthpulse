using System.Net;
using System.Text;

using AwesomeAssertions;
using HealthTracker.Web.Logging;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HealthTracker.Web.Tests;

public sealed class WebServiceRegressionTests
{
    [Fact]
    public async Task Log_reader_reports_when_the_log_directory_does_not_exist()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var service = new ApplicationLogService(new TestWebHostEnvironment(directory.Path));

        // Act
        var snapshot = await service.ReadForViewerAsync(CancellationToken.None);

        // Assert
        snapshot.Content.Should().Be("No application log entries are available yet.");
        snapshot.FileCount.Should().Be(0);
    }

    [Fact]
    public async Task Log_reader_merges_only_healthpulse_log_files_for_the_viewer()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var logDirectory = Path.Combine(directory.Path, "App_Data", "Logs");
        Directory.CreateDirectory(logDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(logDirectory, "healthpulse-20260824.log"),
            "first entry"
        );
        await File.WriteAllTextAsync(Path.Combine(logDirectory, "unrelated.txt"), "secret");
        var service = new ApplicationLogService(new TestWebHostEnvironment(directory.Path));

        // Act
        var snapshot = await service.ReadForViewerAsync(CancellationToken.None);

        // Assert
        snapshot.FileCount.Should().Be(1);
        snapshot.Content.Should().Contain("healthpulse-20260824.log");
        snapshot.Content.Should().Contain("first entry");
        snapshot.Content.Should().NotContain("secret");
    }

    [Fact]
    public async Task Log_reader_download_is_utf8_encoded()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var logDirectory = Path.Combine(directory.Path, "App_Data", "Logs");
        Directory.CreateDirectory(logDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(logDirectory, "healthpulse-20260824.log"),
            "temperature: 21 °C",
            Encoding.UTF8
        );
        var service = new ApplicationLogService(new TestWebHostEnvironment(directory.Path));

        // Act
        var content = await service.ReadForDownloadAsync(CancellationToken.None);

        // Assert
        Encoding.UTF8.GetString(content).Should().Contain("temperature: 21 °C");
    }

    [Fact]
    public void Rolling_logger_writes_structured_message_and_exception()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var provider = new RollingFileLoggerProvider(
            directory.Path,
            TimeSpan.FromDays(1),
            1024 * 1024
        );
        var logger = provider.CreateLogger("Regression.Tests");

        // Act
        logger.LogError(new InvalidOperationException("broken"), "Failure {Code}", 42);

        // Assert
        var file = Directory.EnumerateFiles(directory.Path, "healthpulse-*.log").Should().ContainSingle().Subject;
        var content = File.ReadAllText(file);
        content.Should().Contain("[ERROR] Regression.Tests");
        content.Should().Contain("Failure 42");
        content.Should().Contain("InvalidOperationException: broken");
    }

    [Fact]
    public void Rolling_logger_rolls_an_existing_file_when_the_size_limit_is_reached()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var currentFile = Path.Combine(
            directory.Path,
            $"healthpulse-{DateTimeOffset.UtcNow:yyyyMMdd}.log"
        );
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(currentFile, "old entry");
        using var provider = new RollingFileLoggerProvider(
            directory.Path,
            TimeSpan.FromDays(1),
            1
        );
        var logger = provider.CreateLogger("Regression.Tests");

        // Act
        logger.LogInformation("new entry");

        // Assert
        var files = Directory.EnumerateFiles(directory.Path, "healthpulse-*.log").ToArray();
        files.Should().HaveCount(2);
        files.Select(File.ReadAllText).Should().Contain(text => text.Contains("old entry"));
        files.Select(File.ReadAllText).Should().Contain(text => text.Contains("new entry"));
    }

    [Fact]
    public async Task Configured_mobile_release_is_returned_without_network_access()
    {
        // Arrange
        var configuration = Configuration(
            new Dictionary<string, string?>
            {
                ["Mobile:Android:LatestVersion"] = "2.0.0",
                ["Mobile:Android:ApkUrl"] = "https://example.test/HealthPulse-2.0.0.apk",
                ["Mobile:Android:ReleaseNotes"] = "Important fixes",
            }
        );
        using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException()))
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        var service = new MobileReleaseService(
            client,
            configuration,
            new MemoryCache(new MemoryCacheOptions())
        );

        // Act
        var release = await service.GetLatestAsync(CancellationToken.None);

        // Assert
        release.LatestVersion.Should().Be("2.0.0");
        release.ApkUrl.Should().Be("https://example.test/HealthPulse-2.0.0.apk");
        release.ReleaseNotes.Should().Be("Important fixes");
    }

    [Fact]
    public async Task Invalid_release_repository_is_reported_as_unavailable()
    {
        // Arrange
        var configuration = Configuration(
            new Dictionary<string, string?>
            {
                ["Mobile:Android:ReleaseRepository"] = "not a repository",
            }
        );
        using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException()));
        var service = new MobileReleaseService(
            client,
            configuration,
            new MemoryCache(new MemoryCacheOptions())
        );

        // Act
        var release = await service.GetLatestAsync(CancellationToken.None);

        // Assert
        release.Should().Be(MobileAndroidRelease.Unavailable);
    }

    [Fact]
    public async Task GitHub_release_response_selects_the_matching_android_asset()
    {
        // Arrange
        var configuration = Configuration(
            new Dictionary<string, string?>
            {
                ["Mobile:Android:ReleaseRepository"] = "healthpulse/mobile",
            }
        );
        using var client = new HttpClient(
            new StubHandler(request =>
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri!.PathAndQuery.Should().Be(
                    "/repos/healthpulse/mobile/releases/latest"
                );
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"tag_name\":\"android-v3.1.4\",\"body\":\"Notes\",\"assets\":[{\"name\":\"other.apk\",\"browser_download_url\":\"https://example.test/other.apk\"},{\"name\":\"HealthPulse-3.1.4.apk\",\"browser_download_url\":\"https://example.test/HealthPulse-3.1.4.apk\"}]}"
                    ),
                };
            })
        )
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        var service = new MobileReleaseService(
            client,
            configuration,
            new MemoryCache(new MemoryCacheOptions())
        );

        // Act
        var release = await service.GetLatestAsync(CancellationToken.None);

        // Assert
        release.LatestVersion.Should().Be("3.1.4");
        release.ApkUrl.Should().Be("https://example.test/HealthPulse-3.1.4.apk");
        release.ReleaseNotes.Should().Be("Notes");
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(responseFactory(request));
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "HealthTracker.Web.Tests";
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "HealthPulseTests",
                $"web-services-{Guid.NewGuid():N}"
            );
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
