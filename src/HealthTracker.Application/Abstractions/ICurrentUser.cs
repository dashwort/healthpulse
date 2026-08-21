namespace HealthTracker.Application.Abstractions
{
    public interface ICurrentUser
    {
        string Subject
        {
            get;
        }
        string DisplayName
        {
            get;
        }

        string Email
        {
            get;
        }
    }
}
