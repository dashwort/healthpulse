using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;

namespace HealthTracker.Web.Mcp
{
    /// <summary>Applies post-authentication MCP limits and stores metadata-only audit events.</summary>
    public sealed class McpAuditAndDailyLimitMiddleware(RequestDelegate next)
    {
        private static readonly ConcurrentDictionary<Guid, MinuteWindow> MinuteWindows = new();

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

            var methods = await GetMethodsAsync(context);
            if (!TryAcquireMinutePermit(tokenId) || await dataStore.CountMcpCallsSinceAsync(tokenId, DateTimeOffset.UtcNow.Date, context.RequestAborted) >= 1_000)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("MCP token rate limit exceeded.", context.RequestAborted);
                await WriteAuditAsync(dataStore, tokenId, user.Id, methods, "rate_limited", context.RequestAborted);
                return;
            }

            var originalBody = context.Response.Body;
            await using var capturedBody = new MemoryStream();
            context.Response.Body = capturedBody;
            try
            {
                await next(context);
                var outcome = await GetOutcomeAsync(context.Response.StatusCode, capturedBody, context.RequestAborted);
                await WriteAuditAsync(dataStore, tokenId, user.Id, methods, outcome, context.RequestAborted);
                capturedBody.Position = 0;
                await capturedBody.CopyToAsync(originalBody, context.RequestAborted);
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }

        private static bool TryAcquireMinutePermit(Guid tokenId)
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

                if (window.RequestCount >= 60)
                {
                    return false;
                }

                window.RequestCount++;
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

            context.Request.EnableBuffering();
            try
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
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
                return [.. root.EnumerateArray().Select(ExtractMethod)];
            }

            return [ExtractMethod(root)];
        }

        private static string ExtractMethod(JsonElement request)
        {
            if (!request.TryGetProperty("method", out var method))
            {
                return "mcp";
            }

            if (method.GetString() == "tools/call" && request.TryGetProperty("params", out var parameters) && parameters.TryGetProperty("name", out var toolName))
            {
                return toolName.GetString() ?? "tools/call";
            }

            return method.GetString() ?? "mcp";
        }

        private static async Task<string> GetOutcomeAsync(int statusCode, MemoryStream response, CancellationToken ct)
        {
            if (statusCode >= 400)
            {
                return "failure";
            }

            response.Position = 0;
            try
            {
                using var document = await JsonDocument.ParseAsync(response, cancellationToken: ct);
                return ContainsJsonRpcError(document.RootElement) ? "failure" : "success";
            }
            catch (JsonException)
            {
                return "success";
            }
        }

        private static bool ContainsJsonRpcError(JsonElement root) => root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray().Any(ContainsJsonRpcError),
            JsonValueKind.Object => root.TryGetProperty("error", out _)
                || (root.TryGetProperty("result", out var result) && result.TryGetProperty("isError", out var isError) && isError.GetBoolean()),
            _ => false,
        };

        private static async Task WriteAuditAsync(IHealthDataStore dataStore, Guid tokenId, Guid userId, IReadOnlyCollection<string> methods, string outcome, CancellationToken ct)
        {
            foreach (var method in methods)
            {
                await dataStore.AddMcpAuditLogAsync(new McpAuditLog
                {
                    PersonalAccessTokenId = tokenId,
                    AllowedUserId = userId,
                    Method = method,
                    Outcome = outcome,
                }, ct);
            }

            await dataStore.SaveChangesAsync(ct);
        }

        private sealed class MinuteWindow(DateTimeOffset startUtc)
        {
            public DateTimeOffset StartUtc { get; set; } = startUtc;
            public int RequestCount
            {
                get; set;
            }
        }
    }
}
