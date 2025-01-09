namespace BlogArray.SaaS.Domain.DTOs;

public class CacheConfiguration
{
    public string? Type { get; set; }

    public int AbsoluteExpirationInHours { get; set; }

    public int SlidingExpirationInMinutes { get; set; }

    public string? ConnectionString { get; set; }
}
