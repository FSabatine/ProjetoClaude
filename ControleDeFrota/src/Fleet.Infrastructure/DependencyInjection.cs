using Fleet.Application.Auth;
using Fleet.Application.Common;
using Fleet.Infrastructure.Persistence;
using Fleet.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Fleet";

    public static IServiceCollection AddFleetInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<FleetDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(typeof(FleetDbContext).Assembly.FullName).EnableRetryOnFailure()));
        services.AddScoped<IFleetDbContext>(sp => sp.GetRequiredService<FleetDbContext>());

        services.AddFleetInfrastructureServices(configuration);
        return services;
    }

    /// <summary>Everything except the database provider — reused by tests that plug in SQLite.</summary>
    public static IServiceCollection AddFleetInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= JwtOptions.MinimumKeyBytes, $"Jwt:SigningKey must have at least {JwtOptions.MinimumKeyBytes} characters.")
            .ValidateOnStart();
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<DevDataSeeder>();
        return services;
    }
}
