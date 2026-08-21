using System.Reflection;
using System.Security.Claims;
using System.Text;

using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;
using HealthTracker.Web.Mcp;

using Microsoft.AspNetCore.Http;

namespace HealthTracker.Application.Tests
{
    public sealed class McpAuditAndDailyLimitMiddlewareTests
    {
        [Fact]
        public async Task Oversized_batch_is_rejected_before_the_mcp_handler_runs()
        {
            var store = CreateStore();
            var tokenId = Guid.NewGuid();
            var invoked = false;
            var middleware = new McpAuditAndDailyLimitMiddleware(_ =>
            {
                invoked = true;
                return Task.CompletedTask;
            });
            var context = CreateContext(
                tokenId,
                $"[{string.Join(',', Enumerable.Repeat("{\"method\":\"tools/call\"}", 21))}]"
            );

            await middleware.InvokeAsync(context, store);

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Assert.False(invoked);
        }

        [Fact]
        public async Task Concurrent_near_limit_requests_admit_only_the_remaining_daily_call()
        {
            var store = CreateStore();
            var tokenId = Guid.NewGuid();
            for (var index = 0; index < 999; index++)
            {
                await store.AddMcpAuditLogAsync(
                    new McpAuditLog
                    {
                        PersonalAccessTokenId = tokenId,
                        AllowedUserId = Guid.NewGuid(),
                        Method = "mcp",
                        Outcome = "success",
                    },
                    CancellationToken.None
                );
            }

            var middleware = new McpAuditAndDailyLimitMiddleware(_ => Task.CompletedTask);
            var first = CreateContext(tokenId, "{\"method\":\"sensitive health data\"}");
            var second = CreateContext(tokenId, "{\"method\":\"sensitive health data\"}");

            await Task.WhenAll(
                middleware.InvokeAsync(first, store),
                middleware.InvokeAsync(second, store)
            );

            Assert.True(
                first.Response.StatusCode == StatusCodes.Status200OK
                || second.Response.StatusCode == StatusCodes.Status200OK
            );
            Assert.True(
                first.Response.StatusCode == StatusCodes.Status429TooManyRequests
                || second.Response.StatusCode == StatusCodes.Status429TooManyRequests
            );
            Assert.Equal(1_000, await store.CountMcpCallsSinceAsync(tokenId, DateTimeOffset.UtcNow.Date, CancellationToken.None));
        }

        private static IHealthDataStore CreateStore()
        {
            var testType = typeof(HealthTrackerServiceTests);
            return (IHealthDataStore)Activator.CreateInstance(
                testType.GetNestedType("FakeStore", BindingFlags.NonPublic)!,
                nonPublic: true
            )!;
        }

        private static DefaultHttpContext CreateContext(Guid tokenId, string body)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/mcp";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            context.Response.Body = new MemoryStream();
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Email, "test@example.com"),
                        new Claim("personal_access_token_id", tokenId.ToString()),
                    ],
                    "test"
                )
            );
            return context;
        }
    }
}
