using AwesomeAssertions;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace HealthTracker.Web.Tests;

public sealed class TrafficMetricBufferTests
{
    [Theory]
    [InlineData("api/admin/diagnostics")]
    [InlineData("api/admin/diagnostics/activity")]
    [InlineData("api/admin/diagnostics/traffic/summary")]
    [InlineData("admin/logs/raw")]
    [InlineData("admin/logs/download")]
    public void Diagnostics_requests_are_not_added_to_application_traffic(string routeTemplate)
    {
        // Arrange
        var buffer = new TrafficMetricBuffer();
        var context = ContextFor(routeTemplate);

        // Act
        buffer.Record(context, StatusCodes.Status200OK, 25);

        // Assert
        buffer.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Application_requests_are_added_to_traffic()
    {
        // Arrange
        var buffer = new TrafficMetricBuffer();
        var context = ContextFor("api/readings");

        // Act
        buffer.Record(context, StatusCodes.Status200OK, 25);

        // Assert
        var metric = buffer.Drain().Should().ContainSingle().Subject;
        metric.RouteTemplate.Should().Be("api/readings");
        metric.RequestCount.Should().Be(1);
    }

    private static DefaultHttpContext ContextFor(string routeTemplate)
    {
        var context = new DefaultHttpContext();
        var endpoint = new RouteEndpointBuilder(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(routeTemplate),
            0
        ).Build();
        context.SetEndpoint(endpoint);
        return context;
    }
}
