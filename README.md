# HealthPulse

A .NET 10 Blazor Web App and controller-based REST API for personal health readings. HealthPulse uses hexagonal architecture, EF Core with SQLite as the first adapter, generic OpenID Connect authentication, and MudBlazor for the UI shell.

## Structure

- `src/HealthTracker.Domain` — dependency-free POCO entities and the built-in measurement catalogue.
- `src/HealthTracker.Application` — use cases, DTOs, mappings, normalization, and persistence/user ports.
- `src/HealthTracker.Infrastructure` — EF Core SQLite adapter, database models/mappings, and migrations.
- `src/HealthTracker.Web` — Blazor UI, secured API controllers, OIDC adapter, settings, and deletion worker.

Every data-store operation is user-scoped using the trusted OIDC subject. Controllers never accept a user ID, so one user cannot request another user's templates or readings.

## Configure authentication

Configure `src/HealthTracker.Web/settings.json` (or a non-committed `settings.Development.json`) with your provider's authority, client ID, client secret, callback path, and scopes. For example, Google uses `https://accounts.google.com` as its authority and normally needs `https://localhost:7204/signin-oidc` registered as an authorised redirect URI.

Do not commit a real client secret. In deployed environments, use the equivalent environment variable, for example `Authentication__OpenIdConnect__ClientSecret`. When the app is run in Development with no authority configured, it uses a local development identity only; this fallback cannot activate in production.

## Run and migrate

```powershell
dotnet run --project src/HealthTracker.Web
```

Migrations are applied automatically at startup and built-in templates are seeded. SQLite is the configured adapter; changing provider belongs only in the Infrastructure composition root and EF migration workflow. The application keeps encryption keys in `src/HealthTracker.Web/App_Data/DataProtectionKeys`; deploy this as durable protected storage so sign-in cookies survive restarts.

The EF Core 10 tool (`dotnet-ef` 10.0.11) is installed for the current user. Create future migrations with:

```powershell
dotnet ef migrations add <Name> --project src/HealthTracker.Infrastructure --startup-project src/HealthTracker.Web --context HealthTrackerDbContext --output-dir Persistence/Migrations
```

## API

All endpoints require authentication.

- `GET /api/templates/catalogue` and `GET /api/templates/tracked`
- `POST /api/templates/{templateId}/track`, `DELETE /api/templates/{templateId}/track`
- `POST|PUT|DELETE /api/templates/custom/{templateId?}`
- `GET|POST /api/readings`, `PUT|DELETE /api/readings/{readingId}`

Built-in templates include urate, glucose, HbA1c, lipid measurements, ketones, weight, body fat, waist, temperature, heart rate, oxygen saturation, and separate systolic/diastolic blood pressure. Supported input units are normalized on write for built-in templates. Custom templates preserve their defined unit without conversion.

Deletes are soft deletes. A background worker permanently removes soft-deleted records after 60 days. This app records measurements only; it does not provide medical interpretation or advice.

## Verify

```powershell
dotnet build HealthTracker.slnx
dotnet test HealthTracker.slnx
```
