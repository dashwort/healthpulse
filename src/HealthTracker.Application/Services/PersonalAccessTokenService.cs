using System.Security.Cryptography;
using System.Text;

using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Dtos;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Services
{
    public sealed class PersonalAccessTokenService(IHealthDataStore dataStore, ICurrentUser currentUser)
    {
        private static readonly SemaphoreSlim TokenCreationLock = new(1, 1);
        public async Task<IReadOnlyCollection<PersonalAccessTokenDto>> GetTokensAsync(CancellationToken ct)
        {
            var user = await RequireCurrentAllowedUserAsync(ct);
            return [.. (await dataStore.GetTokensAsync(user.Id, ct)).Select(ToDto)];
        }

        public async Task<CreatedPersonalAccessTokenDto> CreateTokenAsync(string name, CancellationToken ct)
        {
            var user = await RequireCurrentAllowedUserAsync(ct);
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            {
                throw new InvalidOperationException("A token name within 100 characters is required.");
            }
            await TokenCreationLock.WaitAsync(ct);
            try
            {
                return await dataStore.ExecuteInTransactionAsync(async () =>
                {
                    if (await dataStore.CountActiveTokensAsync(user.Id, ct) >= 5)
                    {
                        throw new InvalidOperationException("A user can have at most five active access tokens.");
                    }

                    var secret = $"hp_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
                    var token = new PersonalAccessToken
                    {
                        AllowedUserId = user.Id,
                        Name = name.Trim(),
                        Prefix = secret[..11],
                        Hash = Hash(secret),
                        ExpiresUtc = DateTimeOffset.UtcNow.AddYears(1),
                    };
                    await dataStore.AddTokenAsync(token, ct);
                    await dataStore.SaveChangesAsync(ct);
                    return new CreatedPersonalAccessTokenDto(ToDto(token), secret);
                }, ct);
            }
            finally
            {
                TokenCreationLock.Release();
            }
        }

        public async Task<IReadOnlyCollection<PersonalAccessTokenDto>> GetTokensForUserAsync(Guid allowedUserId, CancellationToken ct)
        {
            var current = await RequireCurrentAllowedUserAsync(ct);
            if (current.Role != AllowedUserRole.Admin && current.Id != allowedUserId)
            {
                throw new UnauthorizedAccessException();
            }

            return [.. (await dataStore.GetTokensAsync(allowedUserId, ct)).Select(ToDto)];
        }

        public async Task RevokeTokenAsync(Guid tokenId, Guid? allowedUserId, CancellationToken ct)
        {
            var current = await RequireCurrentAllowedUserAsync(ct);
            var ownerId = allowedUserId ?? current.Id;
            if (ownerId != current.Id && current.Role != AllowedUserRole.Admin)
            {
                throw new UnauthorizedAccessException();
            }
            var token = (await dataStore.GetTokensAsync(ownerId, ct)).SingleOrDefault(x => x.Id == tokenId)
                ?? throw new KeyNotFoundException("Token not found.");
            token.RevokedUtc = DateTimeOffset.UtcNow;
            await dataStore.UpdateTokenAsync(token, ct);
            await dataStore.SaveChangesAsync(ct);
        }

        public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

        private async Task<AllowedUser> RequireCurrentAllowedUserAsync(CancellationToken ct)
        {
            var email = currentUser.Email.Trim().ToUpperInvariant();
            return await dataStore.FindAllowedUserByEmailAsync(email, false, ct)
                ?? throw new UnauthorizedAccessException();
        }

        private static PersonalAccessTokenDto ToDto(PersonalAccessToken token) =>
            new(token.Id, token.Name, token.Prefix, token.ExpiresUtc, token.LastUsedUtc, token.RevokedUtc.HasValue);
    }
}
