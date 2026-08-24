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

The container listens on port `8080`. The image declares every production-required application setting as an environment variable; credential and administrator values are intentionally empty and must be supplied at runtime. Environment variables override the JSON defaults.

Required application settings:

- `ConnectionStrings__HealthTracker` (the image provides the SQLite default shown below)
- `Authentication__OpenIdConnect__Authority`
- `Authentication__OpenIdConnect__ClientId`
- `Authentication__OpenIdConnect__ClientSecret`

Required on the first start of an empty database:

- `AccessControl__InitialAdministratorEmail`

The image supplies `ConnectionStrings__HealthTracker=Data Source=/app/App_Data/healthtracker.db`, `ASPNETCORE_ENVIRONMENT=Production`, and `ASPNETCORE_HTTP_PORTS=8080` as safe defaults. `Authentication__OpenIdConnect__CallbackPath` defaults to `/signin-oidc`, and the default scopes are `openid`, `profile`, and `email`; these can also be overridden with the corresponding variables shown in `.env.example`. For Google, use `https://accounts.google.com` as the authority and register `http://localhost:8081/signin-oidc` for local OAuth testing (or the equivalent public HTTPS callback). Mount durable storage at `/app/App_Data` to retain the SQLite database and ASP.NET Core data-protection keys across container replacements.

The web app's **App information** page shows the deployed application version, GitHub Actions build number, commit SHA, and UTC build time. It also provides a download link for the latest published Android APK. CI injects the deployment metadata into each production image; locally built containers show `development` / `local` values.

Application diagnostics are written to rolling UTF-8 text files under `/app/App_Data/Logs` and are retained for 14 days by default, with a 10 MB limit per active daily file. The `Logging:File:RetentionDays` and `Logging:File:MaximumFileSizeBytes` settings can override those limits. Administrators can open **Application logs** in the web app, use the direct text view, or download the retained output as `healthpulse-logs.txt`. The direct links are ordinary authenticated HTTP requests, so they remain useful when an interactive Blazor circuit is disconnected. Request logging excludes query strings, headers, cookies, and request bodies; do not treat administrator-only log output as public data.

### Android releases

Tag a release commit as `android-v<major>.<minor>.<patch>` (for example, `android-v1.0.0`). The **Android release** GitHub Actions workflow validates the Android project, builds a signed release APK, verifies its signature, and uploads it as a GitHub Release asset named `HealthPulse-<version>.apk`. It also preserves the APK as a workflow artifact.

The workflow requires these GitHub Actions secrets:

- `ANDROID_KEYSTORE_BASE64` — base64-encoded release keystore.
- `ANDROID_KEYSTORE_PASSWORD`
- `ANDROID_KEY_ALIAS`
- `ANDROID_KEY_PASSWORD`

The workflow derives a monotonically increasing Android version code from the three-part version: `major * 1,000,000 + minor * 1,000 + patch`. Do not reuse or lower a released version.

The production container does not contain APK files. By default, the deployed application checks the latest public GitHub release for \`dashwort/healthpulse\` and advertises its \`HealthPulse-<version>.apk\` asset automatically. It caches that lookup for five minutes, so no deployment configuration needs to change for an ordinary Android release.

Set \`Mobile__Android__ReleaseRepository\` when you publish from a fork or another repository. The following optional settings override automatic discovery, which is useful for private or self-hosted APK distribution:

```text
Mobile__Android__LatestVersion=1.0.0
Mobile__Android__ApkUrl=https://github.com/dashwort/healthpulse/releases/download/android-v1.0.0/HealthPulse-1.0.0.apk
Mobile__Android__ReleaseNotes=What's new in this release
```

The Android app checks this information at `/.well-known/healthpulse-android-update` and opens the configured URL in the device download flow. This keeps APK assets immutable and separate from the web container image.

Create future migrations with the EF Core 10 tool:

```powershell
dotnet ef migrations add <Name> --project src/HealthTracker.Infrastructure --startup-project src/HealthTracker.Web --context HealthTrackerDbContext --output-dir Persistence/Migrations
```

## API

All endpoints require authentication.

## MCP access

Each user can create up to five personal access tokens from **Access tokens**. Tokens expire after one year, are shown once, and can be revoked immediately by their owner or an administrator. Never share a token in chat, source control, or logs.

Configure Codex or VS Code with the public HTTPS Streamable HTTP endpoint `https://your-host/mcp` and an `Authorization: Bearer <token>` header. MCP requests inherit the token owner's current role and access is immediately denied for revoked tokens or archived users. Members can manage only their own health data; administrator tokens can also manage users and revoke tokens.

The endpoint allows 60 calls per minute and 1,000 calls per token per day, returning HTTP `429` when a limit is reached. It retains one year of metadata-only audit history (token/user identifiers, method, outcome, and timestamp); health values, request arguments, and token secrets are never logged. JSON import is additive and validated transactionally before data is saved.

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
