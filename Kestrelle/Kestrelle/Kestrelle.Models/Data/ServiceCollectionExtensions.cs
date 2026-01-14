using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kestrelle.Models.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKestrelleData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KestrelleDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'KestrelleDb' is missing.");
        }

        services.AddDbContext<KestrelleDbContext>(options =>
        {
            options.UseNpgsql(connectionString, x => x.MigrationsAssembly(typeof(KestrelleDbContext).Assembly.FullName));
        });

        return services;
    }
}
