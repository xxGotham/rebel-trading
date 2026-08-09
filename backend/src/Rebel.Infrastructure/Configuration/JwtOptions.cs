namespace Rebel.Infrastructure.Configuration;

public class JwtOptions
{
    public string Secret { get; set; } = "rebel-trading-dev-secret-key-minimum-32-chars!";
    public string Issuer { get; set; } = "rebel-trading";
    public string Audience { get; set; } = "rebel-trading-clients";
}
