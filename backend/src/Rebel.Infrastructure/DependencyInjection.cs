using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rebel.Core.Abstractions;
using Rebel.Core.Trading;
using Rebel.Infrastructure.Configuration;
using Rebel.Infrastructure.Identity;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Providers.AI;
using Rebel.Infrastructure.Providers.Broker;
using Rebel.Infrastructure.Providers.MarketData;
using Rebel.Infrastructure.Services;

namespace Rebel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default")
            ?? "Host=localhost;Database=rebel_trading;Username=postgres;Password=postgres";
        services.AddDbContext<RebelDbContext>(o => o.UseNpgsql(conn));

        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireNonAlphanumeric = false;
            o.User.RequireUniqueEmail = true;
            o.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<RebelDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<RebelOptions>(config.GetSection("Rebel"));

        services.AddSingleton<IMarketDataProvider, SyntheticMarketDataProvider>();
        services.AddSingleton<IBrokerProvider, SyntheticBrokerProvider>();
        services.AddSingleton<IAIProvider, SyntheticAIProvider>();
        services.AddSingleton<IRulesEngine, RulesEngine>();
        services.AddScoped<TokenService>();

        return services;
    }
}
