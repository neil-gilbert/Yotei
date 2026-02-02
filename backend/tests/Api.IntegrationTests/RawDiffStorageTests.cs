using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yotei.Api.Data;
using Yotei.Api.Storage;

namespace Api.IntegrationTests;

public class RawDiffStorageTests
{
    // Given a database-backed storage provider, when raw diff text is missing, then the diff resolves from storage.
    [Fact]
    public async Task Given_DatabaseStorage_When_RawDiffTextMissing_Then_DiffIsResolvedFromDatabase()
    {
        await using var factory = new DatabaseStorageApiFactory();
        using var client = factory.CreateClient();

        var snapshotId = await CreateSnapshotAsync(client);
        var rawDiffRef = await UploadDiffAsync(client, snapshotId);

        await ClearRawDiffTextAsync(factory.Services, snapshotId, "src/api/payments.cs", rawDiffRef);

        var response = await client.GetAsync($"/raw-diffs/{snapshotId}?path=src/api/payments.cs");
        response.EnsureSuccessStatusCode();

        var diffText = await response.Content.ReadAsStringAsync();
        Assert.Contains("stripe", diffText, StringComparison.OrdinalIgnoreCase);
    }

    // Creates a snapshot via the ingestion endpoint.
    private static async Task<Guid> CreateSnapshotAsync(HttpClient client)
    {
        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 42,
            baseSha = "base",
            headSha = Guid.NewGuid().ToString("N"),
            defaultBranch = "main",
            source = "fixture",
            title = "Raw diff storage"
        };

        var response = await client.PostAsJsonAsync("/ingest/snapshot", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IngestResponse>();
        Assert.NotNull(payload);
        return payload!.SnapshotId;
    }

    // Uploads a diff through the raw diff upload endpoint.
    private static async Task<string> UploadDiffAsync(HttpClient client, Guid snapshotId)
    {
        var uploadRequest = new
        {
            path = "src/api/payments.cs",
            changeType = "modified",
            addedLines = 4,
            deletedLines = 1,
            diff = "@@ -1 +1 @@\n+await httpClient.PostAsync(\"https://api.stripe.com/charge\", payload);\n"
        };

        var response = await client.PostAsJsonAsync(
            $"/snapshots/{snapshotId}/file-changes/upload",
            uploadRequest);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Upload diff failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<RawDiffUploadResponse>();
        Assert.NotNull(payload);
        return payload!.RawDiffRef;
    }

    // Clears stored raw diff text to force retrieval from the storage provider.
    private static async Task ClearRawDiffTextAsync(
        IServiceProvider services,
        Guid snapshotId,
        string path,
        string rawDiffRef)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<YoteiDbContext>();

        var fileChange = await dbContext.FileChanges
            .FirstOrDefaultAsync(change =>
                change.PullRequestSnapshotId == snapshotId &&
                string.Equals(change.Path, path, StringComparison.OrdinalIgnoreCase));

        if (fileChange is null)
        {
            throw new InvalidOperationException($"File change for {path} was not created.");
        }

        fileChange.RawDiffText = null;
        fileChange.RawDiffRef = rawDiffRef;
        await dbContext.SaveChangesAsync();
    }

    private sealed record IngestResponse(Guid SnapshotId, bool Created);
    private sealed record RawDiffUploadResponse(string RawDiffRef);

    private sealed class DatabaseStorageApiFactory : WebApplicationFactory<Program>
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

        // Configures the test host to use the database-backed diff storage provider.
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);

            builder.ConfigureAppConfiguration(configuration =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:Enabled"] = "false",
                    ["Database:ApplyMigrations"] = "false",
                    ["Storage:Provider"] = "Database"
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
                services.AddScoped<IRawDiffStorage, DatabaseRawDiffStorage>();

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<YoteiDbContext>();
                db.Database.EnsureCreated();
                if (!db.Tenants.Any())
                {
                    db.Tenants.Add(new Yotei.Api.Models.Tenant
                    {
                        Name = "Test Tenant",
                        Slug = "test-tenant",
                        Token = TenantToken
                    });
                    db.SaveChanges();
                }
            });
        }
    }
}
