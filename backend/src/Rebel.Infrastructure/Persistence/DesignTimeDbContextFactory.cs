using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rebel.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. Migrations are always generated against
/// Npgsql — Postgres is the deployment target, and the SQLite path creates its
/// schema with EnsureCreated instead of running these migrations.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RebelDbContext>
{
    public RebelDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Rebel")
                               ?? "Host=localhost;Port=5432;Database=rebel;Username=rebel;Password=rebel";

        var options = new DbContextOptionsBuilder<RebelDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RebelDbContext(options);
    }
}
