using System.Security.Cryptography;
using System.Text;

using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;

namespace HealthTracker.Web.Services
{
    public sealed class MobileAuthenticationService(IHealthDataStore dataStore)
    {
        private const int AuthorizationLifetimeMinutes = 10;
        private const int AuthorizationCodeLifetimeMinutes = 2;
        private const int AccessTokenLifetimeHours = 12;
        private const int RefreshTokenLifetimeDays = 30;

        public async Task<MobileAuthorizationRequest> BeginAsync(
            string codeChallenge,
            string state,
            string redirectUri,
            CancellationToken ct
        )
        {
            if (
                codeChallenge.Length is < 43 or > 128
                || state.Length is < 1 or > 512
                || !IsSupportedRedirectUri(redirectUri)
            )
            {
                throw new InvalidOperationException("The mobile authorization request is invalid.");
            }

            var request = new MobileAuthorizationRequest
            {
                CodeChallenge = codeChallenge,
                State = state,
                RedirectUri = redirectUri,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(AuthorizationLifetimeMinutes),
            };
            await dataStore.AddMobileAuthorizationRequestAsync(request, ct);
            await dataStore.SaveChangesAsync(ct);
            return request;
        }

        public async Task<(MobileAuthorizationRequest Request, string AuthorizationCode)> CompleteAsync(
            Guid requestId,
            Guid applicationUserId,
            CancellationToken ct
        )
        {
            var request =
                await dataStore.GetMobileAuthorizationRequestAsync(requestId, ct)
                ?? throw new KeyNotFoundException("The authorization request was not found.");
            if (
                request.ExpiresUtc <= DateTimeOffset.UtcNow
                || request.ConsumedUtc.HasValue
                || !string.IsNullOrWhiteSpace(request.AuthorizationCodeHash)
            )
            {
                throw new InvalidOperationException("The authorization request has expired.");
            }

            var authorizationCode = CreateToken("hpac_");
            request.ApplicationUserId = applicationUserId;
            request.AuthorizationCodeHash = Hash(authorizationCode);
            request.AuthorizationCodeExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(
                AuthorizationCodeLifetimeMinutes
            );
            await dataStore.UpdateMobileAuthorizationRequestAsync(request, ct);
            await dataStore.SaveChangesAsync(ct);
            return (request, authorizationCode);
        }

        public Task<MobileTokenResult> ExchangeAuthorizationCodeAsync(
            string authorizationCode,
            string codeVerifier,
            CancellationToken ct
        )
        {
            return dataStore.ExecuteInTransactionAsync(async () =>
            {
                var request =
                    await dataStore.FindMobileAuthorizationRequestByCodeHashAsync(
                        Hash(authorizationCode),
                        ct
                    )
                    ?? throw new InvalidOperationException("The authorization code is invalid.");
                if (
                    request.ConsumedUtc.HasValue
                    || request.ApplicationUserId is null
                    || request.AuthorizationCodeExpiresUtc <= DateTimeOffset.UtcNow
                    || !FixedTimeEquals(CreateCodeChallenge(codeVerifier), request.CodeChallenge)
                )
                {
                    throw new InvalidOperationException("The authorization code is invalid.");
                }

                request.ConsumedUtc = DateTimeOffset.UtcNow;
                await dataStore.UpdateMobileAuthorizationRequestAsync(request, ct);
                var result = await CreateSessionAsync(request.ApplicationUserId.Value, ct);
                await dataStore.SaveChangesAsync(ct);
                return result;
            }, ct);
        }

        public Task<MobileTokenResult> RefreshAsync(string refreshToken, CancellationToken ct)
        {
            return dataStore.ExecuteInTransactionAsync(async () =>
            {
                var session =
                    await dataStore.FindActiveMobileSessionByRefreshHashAsync(Hash(refreshToken), ct)
                    ?? throw new InvalidOperationException("The refresh token is invalid.");
                if (session.RefreshTokenExpiresUtc <= DateTimeOffset.UtcNow)
                {
                    throw new InvalidOperationException("The refresh token is invalid.");
                }

                var accessToken = CreateToken("hpma_");
                var nextRefreshToken = CreateToken("hpmr_");
                session.AccessTokenHash = Hash(accessToken);
                session.AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddHours(
                    AccessTokenLifetimeHours
                );
                session.RefreshTokenHash = Hash(nextRefreshToken);
                session.RefreshTokenExpiresUtc = DateTimeOffset.UtcNow.AddDays(
                    RefreshTokenLifetimeDays
                );
                session.LastUsedUtc = DateTimeOffset.UtcNow;
                await dataStore.UpdateMobileSessionAsync(session, ct);
                await dataStore.SaveChangesAsync(ct);
                return new MobileTokenResult(
                    accessToken,
                    nextRefreshToken,
                    session.AccessTokenExpiresUtc
                );
            }, ct);
        }

        public static string Hash(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }

        private async Task<MobileTokenResult> CreateSessionAsync(Guid applicationUserId, CancellationToken ct)
        {
            var accessToken = CreateToken("hpma_");
            var refreshToken = CreateToken("hpmr_");
            var session = new MobileSession
            {
                ApplicationUserId = applicationUserId,
                AccessTokenHash = Hash(accessToken),
                AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddHours(AccessTokenLifetimeHours),
                RefreshTokenHash = Hash(refreshToken),
                RefreshTokenExpiresUtc = DateTimeOffset.UtcNow.AddDays(RefreshTokenLifetimeDays),
            };
            await dataStore.AddMobileSessionAsync(session, ct);
            return new MobileTokenResult(accessToken, refreshToken, session.AccessTokenExpiresUtc);
        }

        private static bool IsSupportedRedirectUri(string redirectUri)
        {
            return Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
                && string.Equals(uri.Scheme, "healthpulse", StringComparison.Ordinal)
                && string.Equals(uri.Host, "auth", StringComparison.Ordinal)
                && string.Equals(uri.AbsolutePath, "/callback", StringComparison.Ordinal)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment);
        }

        private static string CreateToken(string prefix)
        {
            return prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            if (codeVerifier.Length is < 43 or > 128)
            {
                return string.Empty;
            }

            return Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(left),
                Encoding.UTF8.GetBytes(right)
            );
        }

        private static string Base64UrlEncode(byte[] value)
        {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }

    public sealed record MobileTokenResult(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset AccessTokenExpiresUtc
    );
}
