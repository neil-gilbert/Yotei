using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yotei.Api.Data;
using Yotei.Api.Features.Ingestion;
using Yotei.Api.Storage;

namespace Api.IntegrationTests;

public class GitHubIngestionEndpointTests
{
    // Given a GitHub ingest request, when posted, then a snapshot is created with file changes.
    [Fact]
    public async Task Given_GitHubIngestRequest_When_Posted_Then_SnapshotCreatedWithFileChanges()
    {
        await using var factory = new GitHubApiFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            owner = "acme",
            name = "payments",
            prNumber = 42
        };

        var response = await client.PostAsJsonAsync("/ingest/github", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"GitHub ingest failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GitHubIngestResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.Created);
        Assert.True(payload.FileChangesCount > 0);

        var snapshot = await client.GetFromJsonAsync<SnapshotDetail>($"/snapshots/{payload.SnapshotId}");
        Assert.NotNull(snapshot);
        Assert.NotEmpty(snapshot!.FileChanges);
    }

    // Given configured GitHub repos, when sync is triggered, then snapshots are created for open PRs.
    [Fact]
    public async Task Given_ConfiguredRepos_When_SyncPosted_Then_SnapshotsCreated()
    {
        await using var factory = new GitHubApiFactory(new[] { "acme/payments" });
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/ingest/github/sync", null);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"GitHub sync failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GitHubSyncResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Repositories);
        Assert.True(payload.PullRequests > 0);
        Assert.True(payload.SnapshotsCreated > 0);
        Assert.Empty(payload.Errors);
    }

    private sealed record GitHubIngestResponse(Guid SnapshotId, bool Created, int FileChangesCount);
    private sealed record GitHubSyncResponse(int Repositories, int PullRequests, int SnapshotsCreated, List<string> Errors);
    private sealed record SnapshotDetail(Guid Id, List<FileChangeItem> FileChanges);
    private sealed record FileChangeItem(string Path, string ChangeType, int AddedLines, int DeletedLines, string? RawDiffRef);

    private sealed class GitHubApiFactory : WebApplicationFactory<Program>
    {
        private const string TenantToken = "test-tenant-token";
        private readonly string _databaseName = $"yotei-tests-{Guid.NewGuid()}";
        private readonly InMemoryDatabaseRoot _databaseRoot = new();
        private readonly ServiceProvider _internalProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();
        private readonly string[] _repos;

        public GitHubApiFactory(string[]? repos = null)
        {
            _repos = repos ?? [];
        }

        // Adds the test tenant header to all requests.
        protected override void ConfigureClient(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Token", TenantToken);
        }

        // Configures the test host with a stubbed GitHub API client.
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(AppContext.BaseDirectory);

            builder.ConfigureAppConfiguration(configuration =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Seed:Enabled"] = "false",
                    ["Database:ApplyMigrations"] = "false",
                    ["GitHub:BaseUrl"] = "https://github.local",
                    ["GitHub:Token"] = "test"
                };

                for (var i = 0; i < _repos.Length; i++)
                {
                    settings[$"GitHub:Repos:{i}"] = _repos[i];
                }

                configuration.AddInMemoryCollection(settings);
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

                services.RemoveAll<IGithubIngestionService>();
                services.AddHttpClient<IGithubIngestionService, GithubIngestionService>()
                    .ConfigurePrimaryHttpMessageHandler(() => new StubGitHubHandler());

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

    private sealed class FakeRawDiffStorage : IRawDiffStorage
    {
        private readonly Dictionary<string, string> _diffs = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> StoreDiffAsync(Guid snapshotId, string path, string diff, CancellationToken cancellationToken)
        {
            var safePath = path.Replace(' ', '-');
            var rawDiffRef = $"memory://{snapshotId}/{safePath}";
            _diffs[rawDiffRef] = diff;
            return Task.FromResult(rawDiffRef);
        }

        public Task<string?> GetDiffAsync(string rawDiffRef, CancellationToken cancellationToken)
        {
            _diffs.TryGetValue(rawDiffRef, out var diff);
            return Task.FromResult(diff);
        }
    }

    private sealed class StubGitHubHandler : HttpMessageHandler
    {
        // Returns canned responses for GitHub API endpoints.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var response = path switch
            {
                var value when value.EndsWith("/pulls/42", StringComparison.OrdinalIgnoreCase) =>
                    CreateJsonResponse(PullRequestJson(42)),
                var value when value.Contains("/pulls/42/files", StringComparison.OrdinalIgnoreCase) =>
                    CreateJsonResponse(PullRequestFilesJson),
                var value when value.EndsWith("/pulls", StringComparison.OrdinalIgnoreCase) =>
                    CreateJsonResponse(OpenPullsJson),
                var value when value.Contains("/pulls/77/files", StringComparison.OrdinalIgnoreCase) =>
                    CreateJsonResponse(PullRequestFilesJson),
                var value when value.EndsWith("/pulls/77", StringComparison.OrdinalIgnoreCase) =>
                    CreateJsonResponse(PullRequestJson(77)),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };

            return Task.FromResult(response);
        }

        // Builds a JSON response for a pull request payload.
        private static string PullRequestJson(int number)
        {
            var payload = new
            {
                number,
                title = "GitHub ingest",
                @base = new { sha = $"base-{number}", @ref = "main" },
                head = new { sha = $"head-{number}", @ref = "feature" }
            };

            return JsonSerializer.Serialize(payload);
        }

        private static HttpResponseMessage CreateJsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        private const string PullRequestFilesJson =
            "[{\"filename\":\"src/api/payments.cs\",\"status\":\"modified\",\"additions\":3,\"deletions\":1,\"patch\":\"@@ -1 +1 @@\\n+await httpClient.PostAsync(\\\"https://api.stripe.com/charge\\\", payload);\\n\"}]";

        private const string OpenPullsJson =
            "[{\"number\":77,\"base\":{\"sha\":\"base-77\",\"ref\":\"main\"},\"head\":{\"sha\":\"head-77\",\"ref\":\"feature\"}}]";
    }
}
