using System.Security.Claims;
using System.Text;

using AwesomeAssertions;
using HealthTracker.Domain.Models;
using HealthTracker.Testing;
using HealthTracker.Web.Mcp;

using Microsoft.AspNetCore.Http;

namespace HealthTracker.Application.Tests;

public sealed class McpAuditAndDailyLimitMiddlewareTests
{
    [Fact]
    public async Task Oversized_batch_is_rejected_before_the_mcp_handler_runs()
    {
        // Arrange
        var store = new TestDataStore();
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

        // Act
        await middleware.InvokeAsync(context, store);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task Concurrent_near_limit_requests_admit_only_the_remaining_daily_call()
    {
        // Arrange
        var store = new TestDataStore();
        var tokenId = Guid.NewGuid();
        for (var index = 0; index < 999; index++)
        {
            await store.AddMcpAuditLogAsync(
                new McpAuditLog
                {
                    PersonalAccessTokenId = tokenId,
                    AllowedUserId = store.CurrentAllowedUser.Id,
                    Method = "mcp",
                    Outcome = "success",
                },
                CancellationToken.None
            );
        }

        var middleware = new McpAuditAndDailyLimitMiddleware(_ => Task.CompletedTask);
        var first = CreateContext(tokenId, "{\"method\":\"tools/call\"}");
        var second = CreateContext(tokenId, "{\"method\":\"tools/call\"}");

        // Act
        await Task.WhenAll(
            middleware.InvokeAsync(first, store),
            middleware.InvokeAsync(second, store)
        );

        // Assert
        new[] { first.Response.StatusCode, second.Response.StatusCode }
            .Should()
            .Contain(StatusCodes.Status200OK)
            .And.Contain(StatusCodes.Status429TooManyRequests);
        (await store.CountMcpCallsSinceAsync(tokenId, DateTimeOffset.UtcNow.Date, CancellationToken.None))
            .Should()
            .Be(1_000);
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
