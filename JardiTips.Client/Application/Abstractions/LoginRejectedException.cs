namespace JardiTips.Client.Application.Abstractions;

public sealed class LoginRejectedException : Exception
{
    public LoginRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
