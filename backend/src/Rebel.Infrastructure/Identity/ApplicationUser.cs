using Microsoft.AspNetCore.Identity;
using Rebel.Core.Domain;

namespace Rebel.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.FounderBeta;
    public List<string> Entitlements { get; set; } =
    [
        "LiveCoaching", "VoiceCoaching", "MaxPlaybooks", "SessionHistory",
        "AdvancedJournaling", "MultipleBrokerConnections", "MultipleMarketDataProviders",
        "MultipleAIProviders", "AdvancedAnalytics",
    ];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
