namespace BlogArray.SaaS.Identity.Models;

public class Application
{
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string? LogoutUri { get; set; }
    public List<string>? Users { get; set; }
}

public class OpenIddictApplications
{
    public List<Application> Applications { get; set; }
}
