namespace HealthTracker.Domain.Models
{
    /// <summary>Stable, seeded template definitions. Values are clinical measurements, not clinical advice.</summary>
    public static class BuiltInTemplates
    {
        public static readonly IReadOnlyCollection<MeasurementTemplate> All =
        [
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d01",
                "urate",
                "Urate",
                "Blood chemistry",
                "Urate",
                "mmol/L",
                ["mmol/L", "mg/dL"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d02",
                "glucose",
                "Blood glucose",
                "Blood chemistry",
                "Glucose",
                "mmol/L",
                ["mmol/L", "mg/dL"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d03",
                "hba1c",
                "HbA1c",
                "Blood chemistry",
                "HbA1c",
                "%",
                ["%", "mmol/mol"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d04",
                "total-cholesterol",
                "Total cholesterol",
                "Blood chemistry",
                "Cholesterol",
                "mmol/L",
                ["mmol/L", "mg/dL"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d05",
                "ldl-cholesterol",
                "LDL cholesterol",
                "Blood chemistry",
                "Cholesterol",
                "mmol/L",
                ["mmol/L", "mg/dL"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d06",
                "hdl-cholesterol",
                "HDL cholesterol",
                "Blood chemistry",
                "Cholesterol",
                "mmol/L",
                ["mmol/L", "mg/dL"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d07",
                "triglycerides",
                "Triglycerides",
                "Blood chemistry",
                "Triglycerides",
                "mmol/L",
                ["mmol/L", "mg/dL"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d08",
                "blood-ketones",
                "Blood ketones",
                "Blood chemistry",
                "None",
                "mmol/L",
                ["mmol/L"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d09",
                "weight",
                "Weight",
                "Body measurements",
                "Mass",
                "kg",
                ["kg", "lb"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d10",
                "body-fat",
                "Body fat",
                "Body measurements",
                "None",
                "%",
                ["%"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d11",
                "waist",
                "Waist circumference",
                "Body measurements",
                "Length",
                "cm",
                ["cm", "in"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d12",
                "temperature",
                "Body temperature",
                "Vitals",
                "Temperature",
                "°C",
                ["°C", "°F"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d13",
                "heart-rate",
                "Heart rate",
                "Vitals",
                "None",
                "bpm",
                ["bpm"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d14",
                "oxygen-saturation",
                "Oxygen saturation",
                "Vitals",
                "None",
                "%",
                ["%"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d15",
                "systolic-bp",
                "Systolic blood pressure",
                "Vitals",
                "None",
                "mmHg",
                ["mmHg"]
            ),
            Definition(
                "0b4b7051-b360-4d2d-9f36-0776baf95d16",
                "diastolic-bp",
                "Diastolic blood pressure",
                "Vitals",
                "None",
                "mmHg",
                ["mmHg"]
            ),
        ];

        private static MeasurementTemplate Definition(
            string id,
            string code,
            string name,
            string category,
            string unitCategory,
            string normalizedUnit,
            IReadOnlyCollection<string> allowedUnits
        )
        {
            return new()
            {
                Id = Guid.Parse(id),
                Code = code,
                Name = name,
                Category = category,
                UnitCategory = unitCategory,
                NormalizedUnit = normalizedUnit,
                AllowedUnits = allowedUnits,
                CreatedUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            };
        }
    }
}
