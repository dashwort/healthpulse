using AwesomeAssertions;
using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Tests;

public sealed class UnitConverterTests
{
    [Fact]
    public void Normalize_converts_glucose_mg_per_dl_to_mmol_per_litre()
    {
        // Arrange
        var glucose = BuiltInTemplates.All.Single(item => item.Code == "glucose");

        // Act
        var normalized = UnitConverter.Normalize(180.182m, glucose, "mg/dL");

        // Assert
        normalized.Should().BeApproximately(10m, 0.001m);
    }

    [Fact]
    public void Normalize_rejects_an_unsupported_unit()
    {
        // Arrange
        var urate = BuiltInTemplates.All.Single(item => item.Code == "urate");
        InvalidOperationException? exception = null;

        // Act
        try
        {
            UnitConverter.Normalize(10m, urate, "kg");
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("not supported");
    }

    [Fact]
    public void Urate_uses_umol_per_litre_as_the_default_and_converts_mg_per_dl()
    {
        // Arrange
        var urate = BuiltInTemplates.All.Single(item => item.Code == "urate");

        // Act
        var normalized = UnitConverter.Normalize(16.81m, urate, "mg/dL");

        // Assert
        urate.NormalizedUnit.Should().Be("umol/L");
        urate.AllowedUnits.Should().Equal("umol/L", "mg/dL");
        normalized.Should().BeApproximately(1000m, 0.001m);
    }

    [Fact]
    public void Built_in_catalogue_contains_unique_codes_and_ids()
    {
        // Arrange

        // Act
        var templates = BuiltInTemplates.All.ToArray();

        // Assert
        templates.Should().HaveCount(16);
        templates.Select(item => item.Code).Should().OnlyHaveUniqueItems();
        templates.Select(item => item.Id).Should().OnlyHaveUniqueItems();
    }
}
