using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Yotei.Api.Data;

/// <summary>
/// Provides a design-time DbContext factory for EF tooling without booting the full app.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<YoteiDbContext>
{
    /// <summary>
    /// Creates a DbContext using appsettings and environment variables for migrations.
    /// </summary>
    /// <param name="args">Optional arguments provided by EF tooling.</param>
    /// <returns>A configured <see cref="YoteiDbContext"/> instance.</returns>
    public YoteiDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<YoteiDbContext>();
        var connectionString = configuration.GetConnectionString("Postgres");
        optionsBuilder.UseNpgsql(connectionString);

        return new YoteiDbContext(optionsBuilder.Options);
    }
}
