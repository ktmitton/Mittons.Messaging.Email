namespace Mittons.Messaging.Email.Settings;

public class SmtpSettings
{
    public string Host { get; init; } = default!;
    public int Port { get; init; }
    public bool UseSsl { get; init; }
    public string Username { get; init; } = default!;
    public string Password { get; init; } = default!;
}
