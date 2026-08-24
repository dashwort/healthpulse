using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Tests
{
    public sealed class UnitConverterTests
    {
        [Fact]
        public void Normalize_converts_glucose_mg_per_dl_to_mmol_per_litre()
        {
            var glucose = BuiltInTemplates.All.Single(x => x.Code == "glucose");

            var normalized = UnitConverter.Normalize(180.182m, glucose, "mg/dL");

            Assert.Equal(10m, normalized, 3);
        }

        [Fact]
        public void Normalize_rejects_an_unsupported_unit()
        {
            var urate = BuiltInTemplates.All.Single(x => x.Code == "urate");

            Assert.Throws<InvalidOperationException>(() => UnitConverter.Normalize(10m, urate, "kg"));
        }

        [Fact]
        public void Urate_uses_umol_per_litre_as_the_default_and_converts_mg_per_dl()
        {
            var urate = BuiltInTemplates.All.Single(x => x.Code == "urate");

            Assert.Equal("umol/L", urate.NormalizedUnit);
            Assert.Equal(["umol/L", "mg/dL"], urate.AllowedUnits);
            Assert.Equal(1000m, UnitConverter.Normalize(16.81m, urate, "mg/dL"), 3);
        }
    }
}
