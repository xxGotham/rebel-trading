namespace Rebel.Core.Domain;

public class ProviderConnection
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = default!;
    public string ProviderId { get; set; } = default!;
    public string ProviderName { get; set; } = default!;
    public ProviderType ProviderType { get; set; }
    public ProviderStatus Status { get; set; } = ProviderStatus.Disconnected;
    public KeySource KeySource { get; set; } = KeySource.RebelManaged;
    public string? EncryptedCredentials { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
