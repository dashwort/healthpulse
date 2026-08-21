# HealthPulse

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

HealthPulse is an open-source .NET 10 Blazor Web App and controller-based REST API for recording and visualising personal health readings. It uses hexagonal architecture, EF Core with SQLite as the first adapter, generic OpenID Connect authentication, and MudBlazor for the UI.

Repository: <https://github.com/dashwort/healthpulse>

This project is provided for personal tracking and software development purposes. It does not provide medical interpretation, diagnosis, or treatment advice.

## Structure

- `src/HealthTracker.Domain` — dependency-free POCO entities and the built-in measurement catalogue.
- `src/HealthTracker.Application` — use cases, DTOs, mappings, normalization, and persistence/user ports.
- `src/HealthTracker.Infrastructure` — EF Core SQLite adapter, database models/mappings, and migrations.
- `src/HealthTracker.Web` — Blazor UI, secured API controllers, OIDC adapter, settings, and deletion worker.

Every data-store operation is user-scoped using the trusted OIDC subject. Controllers never accept a user ID, so one user cannot request another user's templates or readings.

## Getting started

### Prerequisites

- .NET SDK 10.0 or later
- An OpenID Connect provider for non-development deployments

Clone the public repository and run the web project:

```powershell
git clone https://github.com/dashwort/healthpulse.git
cd healthpulse
dotnet run --project src/HealthTracker.Web
```

The application applies pending EF Core migrations automatically at startup and seeds the built-in measurement catalogue.

### Configure authentication

Configure the OpenID Connect settings supplied to the web application with your provider's authority, client ID, client secret, callback path, and scopes. For local development, copy `src/HealthTracker.Web/settings.Development.json.example` to an untracked `settings.Development.json`. For example, Google uses `https://accounts.google.com` as its authority.

Never commit a real client secret. In CI/CD or hosted environments, use secret storage or environment variables such as `Authentication__OpenIdConnect__ClientSecret`. When running in Development with no authority configured, the app uses a local development identity; this fallback is disabled outside Development.

HealthPulse is invitation-only. Set `AccessControl__InitialAdministratorEmail` before the first start; it seeds the first administrator, who can then manage approved Google email addresses from User Management. Google sign-in requires a verified email matching an active approved address. Copy `.env.example` to an untracked `.env` for local container settings. Existing local Docker OAuth settings can be inspected from the running container and copied into that ignored file; never add those values to tracked settings or documentation.

### Database and deployment

SQLite is the default adapter and stores data in the configured application data directory. The persistence boundary is database-agnostic; provider changes belong in the Infrastructure composition root and migration workflow. In hosted deployments, persist the ASP.NET Core data-protection keys on durable protected storage so authentication cookies survive restarts.

### Container image

The CI workflow publishes the production image to GitHub Container Registry after successful pushes to `main`:

```text
ghcr.io/dashwort/healthpulse:latest
ghcr.io/dashwort/healthpulse:sha-<commit>
```

The container listens on port `8080`. Supply production OIDC settings through environment variables such as `Authentication__OpenIdConnect__Authority`, `Authentication__OpenIdConnect__ClientId`, and `Authentication__OpenIdConnect__ClientSecret`; environment variables override the JSON defaults. For Google, use `https://accounts.google.com` as the authority and register `http://localhost:8081/signin-oidc` for local OAuth testing (or the equivalent public HTTPS callback). Mount durable storage at `/app/App_Data` to retain the SQLite database and ASP.NET Core data-protection keys across container replacements.

Create future migrations with the EF Core 10 tool:

```powershell
dotnet ef migrations add <Name> --project src/HealthTracker.Infrastructure --startup-project src/HealthTracker.Web --context HealthTrackerDbContext --output-dir Persistence/Migrations
```

## API

All endpoints require authentication.

## MCP access

Each user can create up to five personal access tokens from **Access tokens**. Tokens expire after one year, are shown once, and can be revoked immediately by their owner or an administrator. Configure Codex or VS Code with the public HTTPS Streamable HTTP endpoint `https://your-host/mcp` and an `Authorization: Bearer <token>` header. MCP requests inherit the token owner's current role and access is immediately denied for revoked tokens or archived users. The endpoint is rate limited per token and records one year of metadata-only audit history; health values and tool arguments are never logged.

- `GET /api/templates/catalogue` and `GET /api/templates/tracked`
- `POST /api/templates/{templateId}/track`, `DELETE /api/templates/{templateId}/track`
- `POST|PUT|DELETE /api/templates/custom/{templateId?}`
- `GET|POST /api/readings`, `PUT|DELETE /api/readings/{readingId}`

Built-in templates include urate, glucose, HbA1c, lipid measurements, ketones, weight, body fat, waist, temperature, heart rate, oxygen saturation, and separate systolic/diastolic blood pressure. Supported input units are normalized on write for built-in templates. Custom templates preserve their defined unit without conversion.

Deletes are soft deletes. A background worker permanently removes soft-deleted records after 60 days. This app records measurements only; it does not provide medical interpretation or advice.

## Verify locally

```powershell
dotnet build HealthTracker.slnx
dotnet test HealthTracker.slnx
```
