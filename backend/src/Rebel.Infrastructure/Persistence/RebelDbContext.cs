using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Identity;
using System.Text.Json;

namespace Rebel.Infrastructure.Persistence;

public class RebelDbContext(DbContextOptions<RebelDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Playbook> Playbooks => Set<Playbook>();
    public DbSet<TradingSession> TradingSessions => Set<TradingSession>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<ProviderConnection> ProviderConnections => Set<ProviderConnection>();
    public DbSet<ApiUsageEvent> ApiUsageEvents => Set<ApiUsageEvent>();
    public DbSet<SessionJournal> SessionJournals => Set<SessionJournal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Playbook>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Instruments)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new());
            e.OwnsOne(x => x.Rules, r =>
            {
                r.ToJson();
            });
        });

        builder.Entity<TradingSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Messages);
            e.Ignore(x => x.Events);
        });

        builder.Entity<ConversationMessage>(e => e.HasKey(x => x.Id));
        builder.Entity<ProviderConnection>(e => e.HasKey(x => x.Id));
        builder.Entity<ApiUsageEvent>(e => e.HasKey(x => x.Id));
        builder.Entity<SessionJournal>(e => e.HasKey(x => x.Id));
    }
}
