using System.Security.Claims;
using System.Text.Json;

using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;

namespace HealthTracker.Web.Mcp
{
    public sealed class McpAuditAndDailyLimitMiddleware(RequestDelegate next)
    {
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

            if (await dataStore.CountMcpCallsSinceAsync(tokenId, DateTimeOffset.UtcNow.Date, context.RequestAborted) >= 1000)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Daily MCP token limit exceeded.", context.RequestAborted);
                return;
            }

            var method = await GetMethodAsync(context);
            await next(context);
            var email = context.User.FindFirstValue(ClaimTypes.Email)?.Trim().ToUpperInvariant();
            var user = string.IsNullOrWhiteSpace(email) ? null : await dataStore.FindAllowedUserByEmailAsync(email, false, context.RequestAborted);
            if (user is null) return;
            await dataStore.AddMcpAuditLogAsync(new McpAuditLog
            {
                PersonalAccessTokenId = tokenId,
                AllowedUserId = user.Id,
                Method = method,
                Outcome = context.Response.StatusCode < 400 ? "success" : "failure",
            }, context.RequestAborted);
            await dataStore.SaveChangesAsync(context.RequestAborted);
        }

        private static async Task<string> GetMethodAsync(HttpContext context)
        {
            if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.ContentLength.GetValueOrDefault().Equals(0))
            {
                context.Request.EnableBuffering();
                try
                {
                    using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                    context.Request.Body.Position = 0;
                    var root = document.RootElement;
                    if (root.TryGetProperty("method", out var rpcMethod) && rpcMethod.GetString() == "tools/call" && root.TryGetProperty("params", out var parameters) && parameters.TryGetProperty("name", out var name)) return name.GetString() ?? "tools/call";
                    return rpcMethod.GetString() ?? "mcp";
                }
                catch (JsonException) { context.Request.Body.Position = 0; }
            }
            return "mcp";
        }
    }
}
