using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rebel.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RebelDbContext>
{
    public RebelDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RebelDbContext>()
            .UseNpgsql("Host=localhost;Database=rebel_trading;Username=postgres;Password=postgres")
            .Options;
        return new RebelDbContext(options);
    }
}
