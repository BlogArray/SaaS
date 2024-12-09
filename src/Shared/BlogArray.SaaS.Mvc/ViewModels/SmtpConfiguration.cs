namespace BlogArray.SaaS.Mvc.ViewModels;

public class SmtpConfiguration
{
    public string FromEmail { get; set; } = default!;

    public string FromName { get; set; } = default!;

    public string Username { get; set; } = default!;

    public string Password { get; set; } = default!;

    public string Host { get; set; } = default!;

    public int Port { get; set; } = default!;

    public bool EnableSsl { get; set; } = default!;
}
