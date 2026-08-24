using System.Text;

namespace HealthTracker.Web.Services
{

    public sealed record ApplicationLogSnapshot(
        string Content,
        int FileCount,
        DateTimeOffset GeneratedAtUtc
    );

    /// <summary>
    /// Reads the fixed application log directory. File names are never accepted from a request,
    /// which prevents the diagnostics feature becoming a local file disclosure endpoint.
    /// </summary>
    public sealed class ApplicationLogService(IWebHostEnvironment environment)
    {
        private const int MaximumViewerCharacters = 500_000;
        private const int MaximumDownloadCharacters = 5_000_000;

        private string LogDirectory => Path.Combine(environment.ContentRootPath, "App_Data", "Logs");

        public Task<ApplicationLogSnapshot> ReadForViewerAsync(CancellationToken cancellationToken) =>
            ReadAsync(MaximumViewerCharacters, cancellationToken);

        public async Task<byte[]> ReadForDownloadAsync(CancellationToken cancellationToken)
        {
            var snapshot = await ReadAsync(MaximumDownloadCharacters, cancellationToken);
            return Encoding.UTF8.GetBytes(snapshot.Content);
        }

        private async Task<ApplicationLogSnapshot> ReadAsync(
            int maximumCharacters,
            CancellationToken cancellationToken
        )
        {
            if (!Directory.Exists(LogDirectory))
            {
                return new ApplicationLogSnapshot(
                    "No application log entries are available yet.",
                    0,
                    DateTimeOffset.UtcNow
                );
            }

            var files = Directory
                .EnumerateFiles(LogDirectory, "healthpulse-*.log")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var content = new StringBuilder();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileContent = await ReadFileAsync(file, cancellationToken);
                if (fileContent.Length == 0)
                {
                    continue;
                }

                content.Append("===== ")
                    .Append(Path.GetFileName(file))
                    .AppendLine(" =====")
                    .AppendLine(fileContent);
            }

            if (content.Length == 0)
            {
                content.Append("No application log entries are available yet.");
            }

            if (content.Length > maximumCharacters)
            {
                var omittedCharacters = content.Length - maximumCharacters;
                content.Remove(0, omittedCharacters);
                content.Insert(
                    0,
                    $"[Older log output omitted; showing the latest {maximumCharacters:N0} characters.]\r\n"
                );
            }

            return new ApplicationLogSnapshot(content.ToString(), files.Length, DateTimeOffset.UtcNow);
        }

        private static async Task<string> ReadFileAsync(
            string path,
            CancellationToken cancellationToken
        )
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 16 * 1024,
                useAsync: true
            );
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
