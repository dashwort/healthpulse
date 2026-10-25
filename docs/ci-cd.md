# CI/CD and GitHub Actions

This document describes how HealthPulse's current GitHub Actions workflows run, what they validate, how the web container is published and deployed, and how signed Android APKs are released. The workflow files are the executable source of truth; update this document when their behavior changes.

## Workflows at a glance

| Workflow | Trigger | Main result |
| --- | --- | --- |
| [CI](../.github/workflows/ci.yml) | Every push and pull request; there are no branch, path, or event-type filters in the workflow | Frontend, .NET, and Android validation; container build; production deployment for pushes to `main` |
| [Android release](../.github/workflows/android-release.yml) | A pushed tag matching `android-v*`, or a manual **Run workflow** dispatch with a version | Tests Android, builds and verifies a signed APK, then uploads it as an artifact and GitHub Release asset |

The two workflows are independent. Pushing an `android-v...` tag starts both the general CI workflow and the Android release workflow. General CI builds the container for that tag, but only pushes an image for a push to `main`.

## General CI workflow

### Trigger and job order

The CI workflow runs for pushes to branches or tags and for pull requests. It has no path filters, so documentation-only changes also start the checks. There is no scheduled CI run.

The workflow starts two jobs in parallel:

1. `build-test` runs frontend and .NET checks.
2. `android-test` runs Android unit and emulator tests.

The `container` job waits for both jobs to succeed. The `deploy-production` job only runs after the container job succeeds and only for a push to `main`.

### Frontend and .NET checks (`build-test`)

The job runs on `ubuntu-latest` and performs these steps:

1. Checks out the repository.
2. Installs Node.js 24 and pnpm 11.24.0.
3. Installs frontend dependencies with `pnpm install --frozen-lockfile` in `src/HealthTracker.Web/ClientApp`.
4. Runs `pnpm test` (`rstest run`). The current frontend tests are in `reading-utils.test.ts` and `traffic-formatters.test.ts`.
5. Runs `pnpm build`. This runs the TypeScript check (`tsc --noEmit`) and builds the React app with Rsbuild.
6. Installs .NET SDK 10.0.x, restores `HealthTracker.slnx`, and builds the solution in Release configuration.
7. Installs Chromium and its system dependencies using the Playwright script produced by the web test project.
8. Enforces the repository's reflection-free test rule. The job fails if C# or Kotlin files under `tests` or `android` contain `System.Reflection`, `BindingFlags`, or `Activator.CreateInstance`.
9. Runs `dotnet test HealthTracker.slnx` in Release configuration without rebuilding. It collects XPlat Code Coverage and writes TRX results under `TestResults`.
10. Uploads `TestResults` as `test-results-<run id>` even when earlier steps fail. Missing results are allowed.

The solution includes `HealthTracker.Application.Tests` and `HealthTracker.Web.Tests`. The latter contains API, persistence, web-service, traffic-buffer, and Playwright browser regression tests. The browser tests run against the test web host and Chromium; they exercise representative UI flows such as navigation, quick reading entry, custom-template creation, token creation/revocation, diagnostics navigation, and narrow-screen navigation.

Coverage is collected as an artifact; the workflow does not set a coverage percentage threshold. A failing test or build still fails the job and blocks later jobs.

### Android checks (`android-test`)

This job also runs on `ubuntu-latest` and uses Temurin JDK 21 with Gradle caching:

1. Makes `android/gradlew` executable and runs `./gradlew testDebugUnitTest`.
2. Enables KVM for the emulator.
3. Runs `./gradlew connectedDebugAndroidTest` through `reactivecircus/android-emulator-runner` with Android API 36, Google APIs, x86_64, and a Pixel 2 profile. The emulator is headless and uses software graphics.
4. Uploads the Gradle app test reports and Android instrumented-test results as `android-test-results-<run id>`, including on failure.

The current Android test sources are under `android/app/src/test` (unit tests) and `android/app/src/androidTest` (instrumented tests). The APK's custom Gradle output directory is under `~/.gradle/healthpulse-build/app`.

### Container build and publication (`container`)

This job runs only after both test jobs pass. It uses Docker Buildx to build the root `Dockerfile` with `ghcr.io/dashwort/healthpulse` as the image name.

The Dockerfile has a frontend build stage that runs `pnpm test` and `pnpm build` again before publishing the ASP.NET Core app. This means frontend tests and build are run once in `build-test` and again as part of the container build.

The build receives deployment metadata derived in CI:

- Product version from `<Version>` in `Directory.Build.props`.
- GitHub Actions run number.
- Full commit SHA.
- UTC build timestamp.

The values are passed as Docker build arguments and embedded as deployment environment defaults in the image. They are displayed in the app's App information view.

For every container job, Docker metadata includes a short-SHA tag. The `latest` tag is enabled for the default branch. Publishing is enabled only when the event is a push to `refs/heads/main`; on pull requests and other pushes, the image is built without publishing. On a successful push to `main`, the workflow logs into GHCR with `GITHUB_TOKEN` and publishes tags such as:

```text
ghcr.io/dashwort/healthpulse:latest
ghcr.io/dashwort/healthpulse:sha-<short-commit-sha>
```

The job exposes the pushed image digest for deployment so production can deploy the immutable digest rather than resolving a mutable tag.

### Production deployment (`deploy-production`)

Deployment is limited to successful pushes to `main`. The job depends on `container`, runs on a self-hosted runner with all of these labels:

```text
self-hosted, linux, x64, healthpulse-production
```

It passes the image digest from the container job to:

```bash
sudo /usr/local/sbin/healthpulse-deploy "$IMAGE_DIGEST"
```

The runner host must therefore be online and able to execute that command with `sudo`. The deployment script and runner provisioning are not in this repository, so its exact rollout, health-check, and rollback behavior must be documented and maintained with the host configuration. The deploy job requests `packages: read`; the container job requests `packages: write`.

Pull requests, non-main branch pushes, and Android tag pushes do not deploy production. A failed test, container build, or unavailable self-hosted runner prevents or fails the deployment job.

## Android release workflow

### Trigger and source revision

The Android release workflow starts when:

- A tag matching `android-v*` is pushed; the workflow checks out the tag's source.
- A user manually dispatches the workflow and supplies a required three-part version such as `1.2.3`; the workflow checks out `main`.

Runs with the same `github.ref_name` are serialized and are not canceled when another run starts. The version must match `major.minor.patch`, and the workflow derives the Android version code as:

```text
major * 1,000,000 + minor * 1,000 + patch
```

The result must be between 1 and 2,100,000,000. Keep release versions increasing. The workflow validates the format and range, but does not compare the requested version against previous releases. If the GitHub Release already exists, the workflow replaces an asset of the same APK filename (`--clobber`).

### Test, sign, and publish sequence

On `ubuntu-latest`, the release job:

1. Checks out the release tag or `main` for a manual run.
2. Sets up Temurin JDK 21 and Gradle caching.
3. Validates the version and derives the version code.
4. Runs Android unit tests and the API 36 emulator instrumented tests, as in general CI.
5. Decodes the signing keystore into the runner's temporary directory and restricts it to owner read/write permissions.
6. Builds `assembleRelease` with the derived version name and version code.
7. Verifies the APK with Android's `apksigner`.
8. Uploads `HealthPulse-<version>.apk` as a workflow artifact.
9. Creates a GitHub Release with generated notes and the APK, or uploads/replaces the APK asset on an existing release.

The APK is built at `~/.gradle/healthpulse-build/app/outputs/apk/release/app-release.apk` before it is staged for upload. The workflow grants `contents: write` so it can create or update the GitHub Release. The production web container does not include the APK; it discovers published release metadata separately unless optional mobile update settings override that behavior.

### Required GitHub Actions secrets

Configure these secrets at the repository or organization level for the Android release workflow:

| Secret | Purpose |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded Android release keystore |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Signing key alias |
| `ANDROID_KEY_PASSWORD` | Signing key password |

The Gradle release configuration requires all four signing inputs together and refuses a release build without them. Never commit the keystore or its passwords.

## Local verification

Run from the repository root. These commands correspond to the main CI checks; the automated workflows use Linux runners, while the commands below use PowerShell and the Windows Gradle wrapper.

```powershell
Push-Location src/HealthTracker.Web/ClientApp
pnpm install --frozen-lockfile
pnpm test
pnpm build
Pop-Location

dotnet restore HealthTracker.slnx
dotnet build HealthTracker.slnx --configuration Release --no-restore
pwsh tests/HealthTracker.Web.Tests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test HealthTracker.slnx --configuration Release --no-build

$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
Push-Location android
./gradlew.bat testDebugUnitTest
./gradlew.bat connectedDebugAndroidTest
Pop-Location

docker build --tag healthpulse:local .
```

The emulator test requires an available Android emulator/device locally. The container build repeats the frontend test and build inside its frontend stage. To build the debug APK instead of exercising only tests, run `./gradlew.bat assembleDebug` from `android`.

## Results and troubleshooting

- Open the run in GitHub Actions and inspect the failing job and step logs first.
- Download `test-results-<run id>` for .NET TRX and coverage files, and `android-test-results-<run id>` for Gradle and emulator reports. The upload steps use `always()`, so artifacts may still be available after a failure.
- If `container` did not start, inspect `build-test` and `android-test`; both must pass.
- If a `main` deployment did not start, confirm the event was a push to `main`, the container job succeeded, and a runner with the exact labels is available.
- If deployment fails after starting, the Actions log identifies the failure from the host-side `healthpulse-deploy` command. Its recovery procedure is outside this repository.
- Android signing failures usually indicate a missing or mismatched signing secret. The workflow checks that the encoded keystore exists, Gradle validates all signing inputs, and `apksigner` must accept the built APK before upload.
- Android release tests run before signing. A failure prevents both the APK artifact and GitHub Release asset from being published.

## Workflow boundaries

The repository workflows do not define branch protection or required status checks; those are GitHub repository settings. They also do not define a separate staging deployment, scheduled regression suite, test-coverage threshold, or production rollback implementation. Manual browser/device verification described in [AGENTS.md](../AGENTS.md) remains useful for UI changes beyond the automated regression flows.
