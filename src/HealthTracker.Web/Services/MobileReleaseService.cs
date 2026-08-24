using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Caching.Memory;

namespace HealthTracker.Web.Services
{

    public sealed class MobileReleaseService(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache
    )
    {
        private const string CacheKey = "mobile-android-latest-release";
        private static readonly Regex RepositoryName = new(
            "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$",
            RegexOptions.CultureInvariant
        );

        public async Task<MobileAndroidRelease> GetLatestAsync(CancellationToken cancellationToken)
        {
            var configuredRelease = GetConfiguredRelease();
            if (configuredRelease is not null)
            {
                return configuredRelease;
            }

            if (cache.TryGetValue(CacheKey, out MobileAndroidRelease? cachedRelease))
            {
                return cachedRelease!;
            }

            var repository = configuration["Mobile:Android:ReleaseRepository"];
            if (string.IsNullOrWhiteSpace(repository) || !RepositoryName.IsMatch(repository))
            {
                return MobileAndroidRelease.Unavailable;
            }

            var release = await GetLatestGitHubReleaseAsync(repository, cancellationToken);
            cache.Set(CacheKey, release, TimeSpan.FromMinutes(5));
            return release;
        }

        private MobileAndroidRelease? GetConfiguredRelease()
        {
            var version = configuration["Mobile:Android:LatestVersion"];
            var apkUrl = configuration["Mobile:Android:ApkUrl"];
            if (string.IsNullOrWhiteSpace(version) || !Uri.TryCreate(apkUrl, UriKind.Absolute, out _))
            {
                return null;
            }

            return new MobileAndroidRelease(
                version.Trim(),
                apkUrl,
                configuration["Mobile:Android:ReleaseNotes"] ?? string.Empty
            );
        }

        private async Task<MobileAndroidRelease> GetLatestGitHubReleaseAsync(
            string repository,
            CancellationToken cancellationToken
        )
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "repos/" + repository + "/releases/latest"
                );
                request.Headers.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
                );
                request.Headers.UserAgent.ParseAdd("HealthPulse-Android-Update-Check/1.0");

                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );
                if (!response.IsSuccessStatusCode)
                {
                    return MobileAndroidRelease.Unavailable;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (!root.TryGetProperty("tag_name", out var tagNameElement))
                {
                    return MobileAndroidRelease.Unavailable;
                }

                var tagName = tagNameElement.GetString();
                var version = tagName?.StartsWith("android-v", StringComparison.OrdinalIgnoreCase) == true
                    ? tagName["android-v".Length..]
                    : null;
                if (string.IsNullOrWhiteSpace(version))
                {
                    return MobileAndroidRelease.Unavailable;
                }

                var expectedAssetName = "HealthPulse-" + version + ".apk";
                if (!root.TryGetProperty("assets", out var assetsElement))
                {
                    return MobileAndroidRelease.Unavailable;
                }

                string? assetUrl = null;
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    if (
                        asset.TryGetProperty("name", out var nameElement)
                        && string.Equals(
                            nameElement.GetString(),
                            expectedAssetName,
                            StringComparison.Ordinal
                        )
                        && asset.TryGetProperty("browser_download_url", out var urlElement)
                    )
                    {
                        assetUrl = urlElement.GetString();
                        break;
                    }
                }

                if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out _))
                {
                    return MobileAndroidRelease.Unavailable;
                }

                return new MobileAndroidRelease(
                    version,
                    assetUrl,
                    root.TryGetProperty("body", out var bodyElement)
                        ? bodyElement.GetString() ?? string.Empty
                        : string.Empty
                );
            }
            catch (HttpRequestException)
            {
                return MobileAndroidRelease.Unavailable;
            }
            catch (JsonException)
            {
                return MobileAndroidRelease.Unavailable;
            }
        }
    }

    public sealed record MobileAndroidRelease(string LatestVersion, string? ApkUrl, string ReleaseNotes)
    {
        public static readonly MobileAndroidRelease Unavailable = new("0.0.0", null, string.Empty);
    }
}
