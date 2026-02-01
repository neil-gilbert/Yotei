using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Yotei.Api.Data;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.Ingestion;

/// <summary>
/// Pull-based GitHub ingestion service that creates snapshots and file changes.
/// </summary>
public interface IGithubIngestionService
{
    /// <summary>
    /// Ingests a GitHub pull request into a review session snapshot.
    /// </summary>
    /// <param name="request">The GitHub ingestion request parameters.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The ingestion result describing created artifacts.</returns>
    Task<GitHubIngestResult> IngestPullRequestAsync(GitHubIngestRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Syncs configured repositories and ingests discovered pull requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The sync result describing processed repositories and snapshots.</returns>
    Task<GitHubSyncResult> SyncConfiguredReposAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of pull-based GitHub ingestion using the REST API.
/// </summary>
public sealed class GithubIngestionService : IGithubIngestionService
{
    private const int PageSize = 100;
    private readonly HttpClient _httpClient;
    private readonly YoteiDbContext _dbContext;
    private readonly IRawDiffStorage _rawDiffStorage;
    private readonly IGitHubAccessTokenProvider _accessTokenProvider;
    private readonly GitHubSettings _settings;
    private readonly ILogger<GithubIngestionService> _logger;

    /// <summary>
    /// Initializes the service with required dependencies.
    /// </summary>
    /// <param name="httpClient">HTTP client used for GitHub API calls.</param>
    /// <param name="settings">GitHub configuration settings.</param>
    /// <param name="dbContext">Database context for persistence.</param>
    /// <param name="rawDiffStorage">Storage used for raw diff persistence.</param>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public GithubIngestionService(
        HttpClient httpClient,
        IOptions<GitHubSettings> settings,
        YoteiDbContext dbContext,
        IRawDiffStorage rawDiffStorage,
        IGitHubAccessTokenProvider accessTokenProvider,
        ILogger<GithubIngestionService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _dbContext = dbContext;
        _rawDiffStorage = rawDiffStorage;
        _accessTokenProvider = accessTokenProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GitHubIngestResult> IngestPullRequestAsync(GitHubIngestRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var pullRequest = await FetchPullRequestAsync(request, errors, cancellationToken);
        if (pullRequest is null)
        {
            return new GitHubIngestResult(null, false, 0, errors);
        }

        var repo = await GetOrCreateRepositoryAsync(request.Owner, request.Name, pullRequest.Base.Ref, cancellationToken);
        var existingSnapshot = await FindExistingSnapshotAsync(repo, request.PrNumber, pullRequest.Head.Sha, cancellationToken);
        if (existingSnapshot is not null)
        {
            return new GitHubIngestResult(existingSnapshot.Id, false, existingSnapshot.FileChanges.Count, errors);
        }

        var files = await FetchPullRequestFilesAsync(request, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return new GitHubIngestResult(null, false, 0, errors);
        }

        var snapshot = new PullRequestSnapshot
        {
            Id = Guid.NewGuid(),
            Repository = repo,
            PrNumber = request.PrNumber,
            BaseSha = pullRequest.Base.Sha,
            HeadSha = pullRequest.Head.Sha,
            Source = "github",
            Title = pullRequest.Title
        };

        _dbContext.PullRequestSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var file in files)
        {
            var diffText = BuildDiffText(file);
            var rawDiffRef = await _rawDiffStorage.StoreDiffAsync(snapshot.Id, file.FileName, diffText, cancellationToken);

            snapshot.FileChanges.Add(new FileChange
            {
                Path = file.FileName,
                ChangeType = MapChangeType(file.Status),
                AddedLines = file.Additions,
                DeletedLines = file.Deletions,
                RawDiffRef = rawDiffRef,
                RawDiffText = diffText
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GitHubIngestResult(snapshot.Id, true, snapshot.FileChanges.Count, errors);
    }

    /// <inheritdoc />
    public async Task<GitHubSyncResult> SyncConfiguredReposAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var repositories = 0;
        var pullRequests = 0;
        var snapshotsCreated = 0;

        if (_settings.Repos is null || _settings.Repos.Length == 0)
        {
            errors.Add("GitHub:Repos is not configured.");
            return new GitHubSyncResult(repositories, pullRequests, snapshotsCreated, errors);
        }

        foreach (var repoEntry in _settings.Repos)
        {
            var repoDefinition = ParseRepoDefinition(repoEntry, errors);
            if (repoDefinition is null)
            {
                continue;
            }

            repositories++;
            var processedPrs = new List<GithubPullRequestSummary>();

            if (repoDefinition.PrNumber is not null)
            {
                var ingestResult = await IngestPullRequestAsync(
                    new GitHubIngestRequest(repoDefinition.Owner, repoDefinition.Name, repoDefinition.PrNumber.Value),
                    cancellationToken);

                if (ingestResult.Errors.Count > 0)
                {
                    errors.AddRange(ingestResult.Errors);
                }
                else
                {
                    snapshotsCreated += ingestResult.Created ? 1 : 0;
                }

                pullRequests += 1;
            }
            else
            {
                var openPulls = await FetchOpenPullRequestsAsync(repoDefinition, errors, cancellationToken);
                foreach (var pull in openPulls)
                {
                    var ingestResult = await IngestPullRequestAsync(
                        new GitHubIngestRequest(repoDefinition.Owner, repoDefinition.Name, pull.Number),
                        cancellationToken);

                    if (ingestResult.Errors.Count > 0)
                    {
                        errors.AddRange(ingestResult.Errors);
                    }
                    else
                    {
                        snapshotsCreated += ingestResult.Created ? 1 : 0;
                    }

                    pullRequests += 1;
                    processedPrs.Add(pull);
                }
            }

            if (processedPrs.Count > 0)
            {
                await UpsertIngestionCursorAsync(repoDefinition, processedPrs, cancellationToken);
            }
        }

        return new GitHubSyncResult(repositories, pullRequests, snapshotsCreated, errors);
    }

    // Sends an authenticated request to the GitHub API.
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        await _accessTokenProvider.ApplyAuthenticationAsync(request, cancellationToken);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    // Retrieves pull request details from the GitHub API.
    private async Task<GithubPullRequest?> FetchPullRequestAsync(
        GitHubIngestRequest request,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var url = $"/repos/{request.Owner}/{request.Name}/pulls/{request.PrNumber}";
        var response = await SendAsync(HttpMethod.Get, url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            errors.Add($"GitHub pull request fetch failed with status {(int)response.StatusCode}.");
            return null;
        }

        var pullRequest = await response.Content.ReadFromJsonAsync<GithubPullRequest>(cancellationToken: cancellationToken);
        if (pullRequest is null)
        {
            errors.Add("GitHub pull request payload could not be parsed.");
        }

        return pullRequest;
    }

    // Retrieves all files for a pull request using pagination.
    private async Task<List<GithubPullRequestFile>> FetchPullRequestFilesAsync(
        GitHubIngestRequest request,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var files = new List<GithubPullRequestFile>();
        var page = 1;

        while (true)
        {
            var url = $"/repos/{request.Owner}/{request.Name}/pulls/{request.PrNumber}/files?per_page={PageSize}&page={page}";
            var response = await SendAsync(HttpMethod.Get, url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                errors.Add($"GitHub file list fetch failed with status {(int)response.StatusCode}.");
                break;
            }

            var batch = await response.Content.ReadFromJsonAsync<List<GithubPullRequestFile>>(cancellationToken: cancellationToken);
            if (batch is null || batch.Count == 0)
            {
                break;
            }

            files.AddRange(batch);
            if (batch.Count < PageSize)
            {
                break;
            }

            page++;
        }

        return files;
    }

    // Retrieves open pull requests for a repository when syncing.
    private async Task<List<GithubPullRequestSummary>> FetchOpenPullRequestsAsync(
        RepoDefinition repo,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var pulls = new List<GithubPullRequestSummary>();
        var page = 1;

        while (true)
        {
            var url = $"/repos/{repo.Owner}/{repo.Name}/pulls?state=open&per_page={PageSize}&page={page}";
            var response = await SendAsync(HttpMethod.Get, url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                errors.Add($"GitHub pull list fetch failed with status {(int)response.StatusCode}.");
                break;
            }

            var batch = await response.Content.ReadFromJsonAsync<List<GithubPullRequestSummary>>(cancellationToken: cancellationToken);
            if (batch is null || batch.Count == 0)
            {
                break;
            }

            pulls.AddRange(batch);
            if (batch.Count < PageSize)
            {
                break;
            }

            page++;
        }

        return pulls;
    }

    // Ensures a repository exists for ingestion, updating the default branch when provided.
    private async Task<Repository> GetOrCreateRepositoryAsync(
        string owner,
        string name,
        string? defaultBranch,
        CancellationToken cancellationToken)
    {
        var repo = await _dbContext.Repositories
            .FirstOrDefaultAsync(r => r.Owner == owner && r.Name == name, cancellationToken);

        if (repo is null)
        {
            repo = new Repository
            {
                Owner = owner,
                Name = name,
                DefaultBranch = string.IsNullOrWhiteSpace(defaultBranch) ? "main" : defaultBranch
            };
            _dbContext.Repositories.Add(repo);
        }
        else if (!string.IsNullOrWhiteSpace(defaultBranch) && repo.DefaultBranch != defaultBranch)
        {
            repo.DefaultBranch = defaultBranch;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return repo;
    }

    // Finds an existing snapshot for idempotent ingestion behavior.
    private async Task<PullRequestSnapshot?> FindExistingSnapshotAsync(
        Repository repo,
        int prNumber,
        string headSha,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PullRequestSnapshots
            .Include(snapshot => snapshot.FileChanges)
            .Include(snapshot => snapshot.Repository)
            .FirstOrDefaultAsync(snapshot =>
                    snapshot.Repository != null &&
                    snapshot.Repository.Owner == repo.Owner &&
                    snapshot.Repository.Name == repo.Name &&
                    snapshot.PrNumber == prNumber &&
                    snapshot.HeadSha == headSha,
                cancellationToken);
    }

    // Maps GitHub file status to internal change type values.
    private static string MapChangeType(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "added" => "added",
            "removed" => "deleted",
            "renamed" => "modified",
            "modified" => "modified",
            _ => "modified"
        };
    }

    // Builds a diff string from GitHub patch data or a placeholder for binary files.
    private static string BuildDiffText(GithubPullRequestFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.Patch))
        {
            return file.Patch;
        }

        return $"diff unavailable for {file.FileName} ({file.Status})";
    }

    // Parses repository definition strings like "owner/name" or "owner/name#123".
    private static RepoDefinition? ParseRepoDefinition(string entry, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            errors.Add("GitHub repo entry is empty.");
            return null;
        }

        var parts = entry.Split('#', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var repoParts = parts[0].Split('/', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (repoParts.Length != 2)
        {
            errors.Add($"GitHub repo entry '{entry}' is invalid.");
            return null;
        }

        int? prNumber = null;
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out var parsed))
            {
                errors.Add($"GitHub repo entry '{entry}' has invalid pull request number.");
                return null;
            }

            prNumber = parsed;
        }

        return new RepoDefinition(repoParts[0], repoParts[1], prNumber);
    }

    // Upserts ingestion cursor entries for a repository after a sync.
    private async Task UpsertIngestionCursorAsync(
        RepoDefinition repo,
        List<GithubPullRequestSummary> processedPrs,
        CancellationToken cancellationToken)
    {
        var repository = await _dbContext.Repositories
            .FirstOrDefaultAsync(r => r.Owner == repo.Owner && r.Name == repo.Name, cancellationToken);

        if (repository is null)
        {
            return;
        }

        var cursor = await _dbContext.IngestionCursors
            .FirstOrDefaultAsync(c => c.RepositoryId == repository.Id, cancellationToken);

        var latest = processedPrs
            .OrderByDescending(pr => pr.Number)
            .FirstOrDefault();

        if (cursor is null)
        {
            cursor = new IngestionCursor
            {
                RepositoryId = repository.Id,
                LastHeadSha = latest?.Head?.Sha,
                LastPrNumber = latest?.Number,
                LastSyncedAt = DateTimeOffset.UtcNow
            };
            _dbContext.IngestionCursors.Add(cursor);
        }
        else
        {
            cursor.LastHeadSha = latest?.Head?.Sha ?? cursor.LastHeadSha;
            cursor.LastPrNumber = latest?.Number ?? cursor.LastPrNumber;
            cursor.LastSyncedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record RepoDefinition(string Owner, string Name, int? PrNumber);

    private sealed record GithubPullRequest(
        int Number,
        string Title,
        GithubPullRequestRef Base,
        GithubPullRequestRef Head);

    private sealed record GithubPullRequestSummary(
        int Number,
        GithubPullRequestRef Head,
        GithubPullRequestRef Base);

    private sealed record GithubPullRequestRef(
        string Sha,
        [property: JsonPropertyName("ref")] string Ref);

    private sealed record GithubPullRequestFile(
        [property: JsonPropertyName("filename")] string FileName,
        string Status,
        int Additions,
        int Deletions,
        string? Patch);
}
