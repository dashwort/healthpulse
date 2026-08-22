using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;

using Microsoft.AspNetCore.Http.Features;

namespace HealthTracker.Web.Mcp
{
    /// <summary>Applies post-authentication MCP limits and stores metadata-only audit events.</summary>
    public sealed class McpAuditAndDailyLimitMiddleware(RequestDelegate next)
    {
        private const int MaxMcpRequestBytes = 1_048_576;
        private const int MaxBatchCalls = 20;
        private const int DailyCallLimit = 1_000;
        private static readonly ConcurrentDictionary<Guid, MinuteWindow> MinuteWindows = new();
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> DailyQuotaLocks = new();

        public async Task InvokeAsync(HttpContext context, IHealthDataStore dataStore)
        {
            if (!context.Request.Path.StartsWithSegments("/mcp") || context.User.Identity?.IsAuthenticated != true)
            {
                await next(context);
                return;
            }

            if (!Guid.TryParse(context.User.FindFirstValue("personal_access_token_id"), out var tokenId))
            {
                await next(context);
                return;
            }

            var user = await FindCurrentAllowedUserAsync(context, dataStore);
            if (user is null)
            {
                await next(context);
                return;
            }

            IReadOnlyCollection<string> methods;
            try
            {
                ApplyRequestBodyLimit(context);
                methods = await GetMethodsAsync(context);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("MCP request exceeds supported limits.", context.RequestAborted);
                return;
            }

            var auditLogs = await TryReserveQuotaAsync(
                dataStore,
                tokenId,
                user.Id,
                methods,
                context.RequestAborted
            );
            if (auditLogs is null)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("MCP token rate limit exceeded.", context.RequestAborted);
                return;
            }

            try
            {
                await next(context);
            }
            finally
            {
                var outcome = context.Response.StatusCode >= 400 ? "failure" : "success";
                foreach (var auditLog in auditLogs)
                {
                    auditLog.Outcome = outcome;
                    await dataStore.UpdateMcpAuditLogAsync(auditLog, context.RequestAborted);
                }

                await dataStore.SaveChangesAsync(context.RequestAborted);
            }
        }

        private static void ApplyRequestBodyLimit(HttpContext context)
        {
            if (context.Request.ContentLength > MaxMcpRequestBytes)
            {
                throw new InvalidOperationException();
            }

            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is not null && !feature.IsReadOnly)
            {
                feature.MaxRequestBodySize = MaxMcpRequestBytes;
            }
        }

        private static async Task<IReadOnlyCollection<McpAuditLog>?> TryReserveQuotaAsync(
            IHealthDataStore dataStore,
            Guid tokenId,
            Guid userId,
            IReadOnlyCollection<string> methods,
            CancellationToken ct
        )
        {
            var quotaLock = DailyQuotaLocks.GetOrAdd(tokenId, _ => new SemaphoreSlim(1, 1));
            await quotaLock.WaitAsync(ct);
            try
            {
                if (!TryAcquireMinutePermits(tokenId, methods.Count))
                {
                    return null;
                }

                var usedToday = await dataStore.CountMcpCallsSinceAsync(
                    tokenId,
                    DateTimeOffset.UtcNow.Date,
                    ct
                );
                if (usedToday > DailyCallLimit - methods.Count)
                {
                    return null;
                }

                var auditLogs = methods
                    .Select(method => new McpAuditLog
                    {
                        PersonalAccessTokenId = tokenId,
                        AllowedUserId = userId,
                        Method = method,
                        Outcome = "pending",
                    })
                    .ToArray();
                foreach (var auditLog in auditLogs)
                {
                    await dataStore.AddMcpAuditLogAsync(auditLog, ct);
                }

                await dataStore.SaveChangesAsync(ct);
                return auditLogs;
            }
            finally
            {
                quotaLock.Release();
            }
        }

        private static bool TryAcquireMinutePermits(Guid tokenId, int permits)
        {
            var now = DateTimeOffset.UtcNow;
            var window = MinuteWindows.GetOrAdd(tokenId, _ => new MinuteWindow(now));
            lock (window)
            {
                if (now - window.StartUtc >= TimeSpan.FromMinutes(1))
                {
                    window.StartUtc = now;
                    window.RequestCount = 0;
                }

                if (permits > 60 - window.RequestCount)
                {
                    return false;
                }

                window.RequestCount += permits;
                return true;
            }
        }

        private static async Task<AllowedUser?> FindCurrentAllowedUserAsync(HttpContext context, IHealthDataStore dataStore)
        {
            var email = context.User.FindFirstValue(ClaimTypes.Email)?.Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(email)
                ? null
                : await dataStore.FindAllowedUserByEmailAsync(email, false, context.RequestAborted);
        }

        private static async Task<IReadOnlyCollection<string>> GetMethodsAsync(HttpContext context)
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                return ["mcp"];
            }

            context.Request.EnableBuffering(bufferThreshold: MaxMcpRequestBytes, bufferLimit: MaxMcpRequestBytes);
            try
            {
                using var document = await JsonDocument.ParseAsync(
                    context.Request.Body,
                    new JsonDocumentOptions { MaxDepth = 32 },
                    context.RequestAborted
                );
                return ExtractMethods(document.RootElement);
            }
            catch (JsonException)
            {
                return ["mcp"];
            }
            finally
            {
                context.Request.Body.Position = 0;
            }
        }

        private static IReadOnlyCollection<string> ExtractMethods(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() is 0 or > MaxBatchCalls)
                {
                    throw new InvalidOperationException();
                }

                return [.. root.EnumerateArray().Select(ExtractMethod)];
            }

            return [ExtractMethod(root)];
        }

        private static string ExtractMethod(JsonElement request)
        {
            return request.TryGetProperty("method", out var method)
                && method.ValueKind == JsonValueKind.String
                && method.GetString() == "tools/call"
                ? "tools/call"
                : "mcp";
        }

        private sealed class MinuteWindow(DateTimeOffset startUtc)
        {
            public DateTimeOffset StartUtc { get; set; } = startUtc;

            public int RequestCount { get; set; }
        }
    }
}
