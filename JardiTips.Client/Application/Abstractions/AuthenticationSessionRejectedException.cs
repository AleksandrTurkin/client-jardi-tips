namespace JardiTips.Client.Application.Abstractions;

public sealed class AuthenticationSessionRejectedException : Exception
{
    public AuthenticationSessionRejectedException(Exception innerException)
        : base("The persisted authentication session was rejected.", innerException)
    {
    }
}
