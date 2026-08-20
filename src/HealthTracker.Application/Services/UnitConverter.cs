using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Services
{
    public static class UnitConverter
    {
        public static decimal Normalize(decimal value, MeasurementTemplate template, string sourceUnit)
        {
            if (
                template.AllowedUnits.All(unit =>
                    !string.Equals(unit, sourceUnit, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                throw new InvalidOperationException(
                    $"'{sourceUnit}' is not supported for {template.Name}."
                );
            }

            if (string.Equals(sourceUnit, template.NormalizedUnit, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return template.UnitCategory switch
            {
                "Glucose" => value / 18.0182m,
                "Urate" => value / 16.81m,
                "Cholesterol" => value / 38.67m,
                "Triglycerides" => value / 88.57m,
                "HbA1c" => (value + 2.15m) / 10.929m,
                "Mass" => value * 0.45359237m,
                "Length" => value * 2.54m,
                "Temperature" => (value - 32m) * 5m / 9m,
                _ => throw new InvalidOperationException(
                    $"{template.Name} does not support unit conversion."
                ),
            };
        }
    }
}
