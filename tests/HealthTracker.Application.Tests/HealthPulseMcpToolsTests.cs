using System.Reflection;
using System.Text.Json;

using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Services;
using HealthTracker.Web.Mcp;

namespace HealthTracker.Application.Tests
{
    public sealed class HealthPulseMcpToolsTests
    {
        [Fact]
        public async Task Import_json_rejects_more_than_the_supported_template_count()
        {
            var tools = CreateTools();
            var json = JsonSerializer.Serialize(
                new
                {
                    templates = Enumerable
                        .Range(0, 101)
                        .Select(index => new
                        {
                            id = Guid.NewGuid(),
                            name = $"Template {index}",
                            category = "Custom",
                            normalizedUnit = "unit",
                            isCustom = true,
                        }),
                    readings = Array.Empty<object>(),
                }
            );

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => tools.ImportJson(json, CancellationToken.None)
            );
        }

        [Fact]
        public async Task Import_json_allows_a_bounded_empty_export_document()
        {
            var tools = CreateTools();

            var result = await tools.ImportJson(
                "{\"templates\":[],\"readings\":[]}",
                CancellationToken.None
            );

            Assert.Equal("Imported 0 custom templates and 0 readings.", result);
        }

        private static HealthPulseMcpTools CreateTools()
        {
            var testType = typeof(HealthTrackerServiceTests);
            var store = (IHealthDataStore)Activator.CreateInstance(
                testType.GetNestedType("FakeStore", BindingFlags.NonPublic)!,
                nonPublic: true
            )!;
            var currentUser = (ICurrentUser)Activator.CreateInstance(
                testType.GetNestedType("FakeCurrentUser", BindingFlags.NonPublic)!,
                nonPublic: true
            )!;
            return new HealthPulseMcpTools(
                new HealthTrackerService(store, currentUser),
                new PersonalAccessTokenService(store, currentUser)
            );
        }
    }
}
