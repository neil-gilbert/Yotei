using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yotei.Api.Data;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Api.IntegrationTests;

public class TestApiFactory : WebApplicationFactory<Program>
{
    private const string TenantToken = "test-tenant-token";
    private readonly string _databaseName = $"yotei-tests-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly ServiceProvider _internalProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    // Adds the test tenant header to all requests.
    protected override void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("X-Tenant-Token", TenantToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "false",
                ["Database:ApplyMigrations"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<YoteiDbContext>>();
            services.RemoveAll<YoteiDbContext>();
            services.AddDbContext<YoteiDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName, _databaseRoot);
                options.UseInternalServiceProvider(_internalProvider);
            });

            services.RemoveAll<IRawDiffStorage>();
            services.AddSingleton<IRawDiffStorage>(new FakeRawDiffStorage());

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<YoteiDbContext>();
            db.Database.EnsureCreated();
            if (!db.Tenants.Any())
            {
                db.Tenants.Add(new Tenant
                {
                    Name = "Test Tenant",
                    Slug = "test-tenant",
                    Token = TenantToken
                });
                db.SaveChanges();
            }
        });
    }

    private sealed class FakeRawDiffStorage : IRawDiffStorage
    {
        private readonly Dictionary<string, string> _diffs = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> StoreDiffAsync(Guid snapshotId, string path, string diff, CancellationToken cancellationToken)
        {
            var safePath = path.Replace(' ', '-');
            var rawDiffRef = $"s3://fake/{snapshotId}/{safePath}";
            _diffs[rawDiffRef] = diff;
            return Task.FromResult(rawDiffRef);
        }

        public Task<string?> GetDiffAsync(string rawDiffRef, CancellationToken cancellationToken)
        {
            _diffs.TryGetValue(rawDiffRef, out var diff);
            return Task.FromResult(diff);
        }
    }
}
