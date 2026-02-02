using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
    private const string WebhookSecret = "test-webhook-secret";

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

    // Given a pull_request opened webhook, when processed, then a PR comment is posted.
    [Fact]
    public async Task Given_PullRequestOpenedWebhook_When_Processed_Then_CommentPosted()
    {
        await using var factory = new GitHubApiFactory();
        using var client = factory.CreateClient();

        var payload = BuildPullRequestWebhookPayload(GitHubApiFactory.WebhookInstallationId, 42);
        var signature = ComputeSignature(WebhookSecret, payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/ingest/github/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "pull_request");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"GitHub webhook failed: {(int)response.StatusCode} {response.ReasonPhrase} {body}");
        }

        var commentBody = factory.GitHubHandler.CommentBodies.SingleOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(commentBody));
        Assert.Contains("https://yotei.example", commentBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("yotei-logo.png", commentBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tenant=test-tenant-token", commentBody, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GitHubIngestResponse(Guid SnapshotId, bool Created, int FileChangesCount);
    private sealed record GitHubSyncResponse(int Repositories, int PullRequests, int SnapshotsCreated, List<string> Errors);
    private sealed record SnapshotDetail(Guid Id, List<FileChangeItem> FileChanges);
    private sealed record FileChangeItem(string Path, string ChangeType, int AddedLines, int DeletedLines, string? RawDiffRef);

    private sealed class GitHubApiFactory : WebApplicationFactory<Program>
    {
        private const string TenantToken = "test-tenant-token";
        public const long WebhookInstallationId = 1234;
        private readonly string _databaseName = $"yotei-tests-{Guid.NewGuid()}";
        private readonly InMemoryDatabaseRoot _databaseRoot = new();
        private readonly ServiceProvider _internalProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();
        private readonly string[] _repos;
        private readonly StubGitHubHandler _gitHubHandler = new();

        public GitHubApiFactory(string[]? repos = null)
        {
            _repos = repos ?? [];
        }

        // Exposes the stub handler so tests can assert on GitHub API calls.
        public StubGitHubHandler GitHubHandler => _gitHubHandler;

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
                    ["GitHub:Token"] = "test",
                    ["GitHub:App:WebhookSecret"] = WebhookSecret,
                    ["Frontend:BaseUrl"] = "https://yotei.example"
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
                    .ConfigurePrimaryHttpMessageHandler(() => _gitHubHandler);

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<YoteiDbContext>();
                db.Database.EnsureCreated();
                if (!db.Tenants.Any())
                {
                    var tenant = new Yotei.Api.Models.Tenant
                    {
                        Name = "Test Tenant",
                        Slug = "test-tenant",
                        Token = TenantToken
                    };
                    db.Tenants.Add(tenant);
                    db.GitHubInstallations.Add(new Yotei.Api.Models.GitHubInstallation
                    {
                        Tenant = tenant,
                        InstallationId = WebhookInstallationId,
                        AccountLogin = "acme",
                        AccountType = "Organization"
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
        private readonly ConcurrentBag<string> _commentBodies = new();

        public IReadOnlyCollection<string> CommentBodies => _commentBodies.ToArray();

        // Returns canned responses for GitHub API endpoints.
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Method == HttpMethod.Post &&
                path.Contains("/issues/", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith("/comments", StringComparison.OrdinalIgnoreCase))
            {
                var payload = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(payload))
                {
                    using var document = JsonDocument.Parse(payload);
                    if (document.RootElement.TryGetProperty("body", out var bodyElement))
                    {
                        var body = bodyElement.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            _commentBodies.Add(body);
                        }
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"id\":1}", Encoding.UTF8, "application/json")
                };
            }

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

            return response;
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

    // Builds a minimal webhook payload for a pull_request opened event.
    private static string BuildPullRequestWebhookPayload(long installationId, int prNumber)
    {
        var payload = new
        {
            action = "opened",
            pull_request = new { number = prNumber },
            repository = new
            {
                name = "payments",
                owner = new { login = "acme" }
            },
            installation = new { id = installationId }
        };

        return JsonSerializer.Serialize(payload);
    }

    // Computes a GitHub webhook signature for the payload.
    private static string ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
