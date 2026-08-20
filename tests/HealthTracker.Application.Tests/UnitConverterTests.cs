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
    }
}
