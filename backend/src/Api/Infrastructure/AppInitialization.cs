using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yotei.Api.Data;
using Yotei.Api.Seed;
using Yotei.Api.Storage;

namespace Yotei.Api.Infrastructure;

public static class AppInitialization
{
    private const int MigrationRetryCount = 6;

    /// <summary>
    /// Initializes the database and seeds fixture data on startup.
    /// </summary>
    public static async Task InitializeAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<YoteiDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        var applyMigrations = config.GetValue("Database:ApplyMigrations", true);

        await InitializeDatabaseAsync(db, applyMigrations, logger);

        var seedEnabled = config.GetValue<bool>("Seed:Enabled");
        var seedPath = config.GetValue<string>("Seed:FixturePath");
        var seedSettings = new SeedSettings(seedEnabled, seedPath);
        var storage = scope.ServiceProvider.GetRequiredService<IRawDiffStorage>();
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        await FixtureSeeder.SeedAsync(db, storage, app.Environment.ContentRootPath, seedSettings, seedLogger);
    }

    /// <summary>
    /// Applies migrations with retries to handle container startup ordering.
    /// </summary>
    private static async Task InitializeDatabaseAsync(
        YoteiDbContext db,
        bool applyMigrations,
        ILogger logger)
    {
        for (var attempt = 1; attempt <= MigrationRetryCount; attempt++)
        {
            try
            {
                if (!applyMigrations)
                {
                    await db.Database.EnsureCreatedAsync();
                }
                else if (db.Database.IsRelational())
                {
                    await db.Database.MigrateAsync();
                }
                else
                {
                    await db.Database.EnsureCreatedAsync();
                }

                return;
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex) && attempt < MigrationRetryCount)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(10, Math.Pow(2, attempt)));
                logger.LogWarning(ex,
                    "Database not ready, retrying in {DelaySeconds}s (attempt {Attempt}/{Max}).",
                    delay.TotalSeconds,
                    attempt,
                    MigrationRetryCount);
                await Task.Delay(delay);
            }
        }

        if (!applyMigrations)
        {
            await db.Database.EnsureCreatedAsync();
        }
        else if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
    }

    /// <summary>
    /// Identifies transient database startup failures that should be retried.
    /// </summary>
    private static bool IsTransientDatabaseException(Exception exception)
    {
        return exception is NpgsqlException ||
            exception.InnerException is SocketException ||
            exception.InnerException?.InnerException is SocketException;
    }
}
