using System.Text;

namespace HealthTracker.Web.Logging
{

    /// <summary>
    /// Writes the application's structured logger output to small, rolling text files.
    /// The files live under App_Data so a container volume can preserve them across updates.
    /// </summary>
    public sealed class RollingFileLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly string directory;
        private readonly TimeSpan retention;
        private readonly long maximumFileSizeBytes;
        private readonly object writeLock = new();
        private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

        public RollingFileLoggerProvider(string directory, TimeSpan retention, long maximumFileSizeBytes)
        {
            this.directory = directory;
            this.retention = retention;
            this.maximumFileSizeBytes = maximumFileSizeBytes;
        }

        public ILogger CreateLogger(string categoryName) => new RollingFileLogger(this, categoryName);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
            this.scopeProvider = scopeProvider ?? throw new ArgumentNullException(nameof(scopeProvider));

        internal void Write(
            string categoryName,
            LogLevel logLevel,
            EventId eventId,
            string message,
            Exception? exception
        )
        {
            var timestamp = DateTimeOffset.UtcNow;
            var scopes = new List<string>();
            scopeProvider.ForEachScope(
                (scope, state) =>
                {
                    if (scope is not null)
                    {
                        state.Add(scope.ToString() ?? string.Empty);
                    }
                },
                scopes
            );

            var builder = new StringBuilder()
                .Append(timestamp.ToString("O"))
                .Append(" [")
                .Append(logLevel.ToString().ToUpperInvariant())
                .Append("] ")
                .Append(categoryName);

            if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
            {
                builder.Append(" (").Append(eventId.Id);
                if (!string.IsNullOrWhiteSpace(eventId.Name))
                {
                    builder.Append(':').Append(eventId.Name);
                }
                builder.Append(')');
            }

            if (scopes.Count > 0)
            {
                builder.Append(" [").Append(string.Join("] [", scopes)).Append(']');
            }

            builder.Append(" - ").AppendLine(message);
            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            var entry = Encoding.UTF8.GetBytes(builder.ToString());

            try
            {
                lock (writeLock)
                {
                    Directory.CreateDirectory(directory);
                    RemoveExpiredFiles(timestamp.UtcDateTime);

                    var currentFile = Path.Combine(directory, $"healthpulse-{timestamp:yyyyMMdd}.log");
                    if (
                        File.Exists(currentFile)
                        && new FileInfo(currentFile).Length + entry.Length > maximumFileSizeBytes
                    )
                    {
                        var rolledFile = Path.Combine(
                            directory,
                            $"healthpulse-{timestamp:yyyyMMdd}-{timestamp:HHmmssfff}.log"
                        );
                        var suffix = 1;
                        while (File.Exists(rolledFile))
                        {
                            rolledFile = Path.Combine(
                                directory,
                                $"healthpulse-{timestamp:yyyyMMdd}-{timestamp:HHmmssfff}-{suffix++}.log"
                            );
                        }

                        File.Move(currentFile, rolledFile);
                    }

                    using var stream = new FileStream(
                        currentFile,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.ReadWrite
                    );
                    stream.Write(entry, 0, entry.Length);
                }
            }
            catch (Exception loggingException)
            {
                // Logging must never take the application down, and logging this failure through
                // ILogger would recurse into this provider. Keep the fallback deliberately small.
                try
                {
                    Console.Error.WriteLine($"HealthPulse file logging failed: {loggingException.Message}");
                }
                catch
                {
                    // Ignore failures writing to the process error stream during shutdown.
                }
            }
        }

        private void RemoveExpiredFiles(DateTime utcNow)
        {
            var cutoff = utcNow - retention;
            foreach (var path in Directory.EnumerateFiles(directory, "healthpulse-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                    // A concurrently inspected or mounted file can be unavailable briefly.
                }
                catch (UnauthorizedAccessException)
                {
                    // Do not prevent a new entry being written when cleanup is restricted.
                }
            }
        }

        public void Dispose()
        {
        }

        private sealed class RollingFileLogger(
            RollingFileLoggerProvider provider,
            string categoryName
        ) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => provider.scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            )
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                provider.Write(categoryName, logLevel, eventId, formatter(state, exception), exception);
            }
        }
    }
}
