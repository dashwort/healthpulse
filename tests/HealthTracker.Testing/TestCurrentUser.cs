using HealthTracker.Application.Abstractions;

namespace HealthTracker.Testing;

public sealed class TestCurrentUser : ICurrentUser
{
    public TestCurrentUser(
        string subject = "test-user",
        string email = "test@example.com",
        string displayName = "Test user"
    )
    {
        Subject = subject;
        Email = email;
        DisplayName = displayName;
    }

    public string Subject { get; }

    public string DisplayName { get; }

    public string Email { get; }
}
