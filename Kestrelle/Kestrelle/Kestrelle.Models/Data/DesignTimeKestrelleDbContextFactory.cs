using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Kestrelle.Models.Data;

public sealed class DesignTimeKestrelleDbContextFactory : IDesignTimeDbContextFactory<KestrelleDbContext>
{
    public KestrelleDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("KestrelleDb")
                 ?? configuration["ConnectionStrings:KestrelleDb"]
                 ?? throw new Exception("No connection string specified.");

        var options = new DbContextOptionsBuilder<KestrelleDbContext>()
            .UseNpgsql(connectionString, x => x.MigrationsAssembly(typeof(KestrelleDbContext).Assembly.FullName))
            .Options;

        return new KestrelleDbContext(options);
    }
}
