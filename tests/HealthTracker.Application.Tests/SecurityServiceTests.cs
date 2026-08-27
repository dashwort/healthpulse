using System.Security.Cryptography;
using System.Text;

using AwesomeAssertions;
using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;
using HealthTracker.Testing;
using HealthTracker.Web.Services;

namespace HealthTracker.Application.Tests;

public sealed class SecurityServiceTests
{
    [Fact]
    public async Task Personal_access_tokens_are_hashed_expire_in_one_year_and_can_be_listed()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new PersonalAccessTokenService(store, new TestCurrentUser());

        // Act
        var created = await service.CreateTokenAsync("Codex", CancellationToken.None);

        // Assert
        created.Secret.Should().StartWith("hp_");
        created.Secret.Should().NotBe(store.Tokens.Single().Hash);
        PersonalAccessTokenService.Hash(created.Secret).Should().Be(store.Tokens.Single().Hash);
        created.Token.ExpiresUtc.Should().BeCloseTo(DateTimeOffset.UtcNow.AddYears(1), TimeSpan.FromMinutes(1));
        (await service.GetTokensAsync(CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task Personal_access_token_revoke_marks_the_token_revoked()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new PersonalAccessTokenService(store, new TestCurrentUser());
        var token = new PersonalAccessToken
        {
            AllowedUserId = store.CurrentAllowedUser.Id,
            Name = "Existing",
            Prefix = "hp_test",
            Hash = PersonalAccessTokenService.Hash("secret"),
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
        };
        store.Tokens.Add(token);

        // Act
        await service.RevokeTokenAsync(token.Id, null, CancellationToken.None);

        // Assert
        token.RevokedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Personal_access_tokens_enforce_the_five_token_limit()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new PersonalAccessTokenService(store, new TestCurrentUser());
        for (var index = 0; index < 5; index++)
        {
            store.Tokens.Add(
                new PersonalAccessToken
                {
                    AllowedUserId = store.CurrentAllowedUser.Id,
                    Name = $"Token {index}",
                    Prefix = "hp_test",
                    Hash = $"hash-{index}",
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
                }
            );
        }
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.CreateTokenAsync("Too many", CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("five");
    }

    [Fact]
    public async Task Mobile_authorization_begin_persists_the_pkce_request()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new MobileAuthenticationService(store);
        var verifier = new string('a', 64);
        var challenge = CreateCodeChallenge(verifier);

        // Act
        var request = await service.BeginAsync(
            challenge,
            "state",
            "healthpulse://auth/callback",
            CancellationToken.None
        );

        // Assert
        request.CodeChallenge.Should().Be(challenge);
        request.State.Should().Be("state");
        request.RedirectUri.Should().Be("healthpulse://auth/callback");
        store.MobileAuthorizationRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task Mobile_authorization_begin_rejects_an_invalid_redirect_uri()
    {
        // Arrange
        var service = new MobileAuthenticationService(new TestDataStore());
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.BeginAsync(
                new string('a', 43),
                "state",
                "https://example.test/callback",
                CancellationToken.None
            );
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        exception!.Message.Should().Contain("invalid");
    }

    [Fact]
    public async Task Mobile_authorization_exchange_consumes_the_code_and_creates_a_session()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new MobileAuthenticationService(store);
        var verifier = new string('b', 64);
        var code = "hpac_test-code";
        store.MobileAuthorizationRequests.Add(
            new MobileAuthorizationRequest
            {
                CodeChallenge = CreateCodeChallenge(verifier),
                State = "state",
                RedirectUri = "healthpulse://auth/callback",
                ApplicationUserId = store.CurrentUser.Id,
                AuthorizationCodeHash = MobileAuthenticationService.Hash(code),
                AuthorizationCodeExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1),
            }
        );

        // Act
        var result = await service.ExchangeAuthorizationCodeAsync(
            code,
            verifier,
            CancellationToken.None
        );

        // Assert
        result.AccessToken.Should().StartWith("hpma_");
        result.RefreshToken.Should().StartWith("hpmr_");
        store.MobileAuthorizationRequests.Single().ConsumedUtc.Should().NotBeNull();
        store.MobileSessions.Should().ContainSingle();
        store.MobileSessions.Single().AccessTokenHash.Should().Be(
            MobileAuthenticationService.Hash(result.AccessToken)
        );
    }

    [Fact]
    public async Task Mobile_refresh_rotates_both_session_tokens()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new MobileAuthenticationService(store);
        var oldRefreshToken = "hpmr_old";
        store.MobileSessions.Add(
            new MobileSession
            {
                ApplicationUserId = store.CurrentUser.Id,
                AccessTokenHash = MobileAuthenticationService.Hash("old-access"),
                AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
                RefreshTokenHash = MobileAuthenticationService.Hash(oldRefreshToken),
                RefreshTokenExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
            }
        );

        // Act
        var result = await service.RefreshAsync(oldRefreshToken, CancellationToken.None);

        // Assert
        result.AccessToken.Should().StartWith("hpma_");
        result.RefreshToken.Should().StartWith("hpmr_");
        store.MobileSessions.Single().RefreshTokenHash.Should().Be(
            MobileAuthenticationService.Hash(result.RefreshToken)
        );
        result.RefreshToken.Should().NotBe(oldRefreshToken);
    }

    [Fact]
    public async Task Mobile_exchange_rejects_a_code_verifier_that_does_not_match_pkce()
    {
        // Arrange
        var store = new TestDataStore();
        var service = new MobileAuthenticationService(store);
        store.MobileAuthorizationRequests.Add(
            new MobileAuthorizationRequest
            {
                CodeChallenge = CreateCodeChallenge("a".PadRight(64, 'a')),
                State = "state",
                RedirectUri = "healthpulse://auth/callback",
                ApplicationUserId = store.CurrentUser.Id,
                AuthorizationCodeHash = MobileAuthenticationService.Hash("hpac_code"),
                AuthorizationCodeExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1),
            }
        );
        InvalidOperationException? exception = null;

        // Act
        try
        {
            await service.ExchangeAuthorizationCodeAsync(
                "hpac_code",
                "b".PadRight(64, 'b'),
                CancellationToken.None
            );
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
        store.MobileSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Members_cannot_list_another_users_personal_access_tokens()
    {
        // Arrange
        var store = new TestDataStore(role: AllowedUserRole.Member);
        var other = new AllowedUser
        {
            Email = "other@example.com",
            NormalizedEmail = "OTHER@EXAMPLE.COM",
            Role = AllowedUserRole.Member,
        };
        store.AllowedUsers.Add(other);
        var service = new PersonalAccessTokenService(store, new TestCurrentUser());
        UnauthorizedAccessException? exception = null;

        // Act
        try
        {
            await service.GetTokensForUserAsync(other.Id, CancellationToken.None);
        }
        catch (UnauthorizedAccessException ex)
        {
            exception = ex;
        }

        // Assert
        exception.Should().NotBeNull();
    }

    [Fact]
    public void Mobile_hash_is_stable_and_does_not_return_the_secret()
    {
        // Arrange
        const string secret = "secret";

        // Act
        var hash = MobileAuthenticationService.Hash(secret);

        // Assert
        hash.Should().Be(MobileAuthenticationService.Hash(secret));
        hash.Should().NotContain(secret);
        hash.Should().HaveLength(64);
    }

    private static string CreateCodeChallenge(string verifier) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
