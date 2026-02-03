using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Yotei.Api.Data;
using Yotei.Api.Features.Tenancy;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;

namespace Yotei.Api.Features.Ingestion;

public static class IngestionEndpoints
{
    /// <summary>
    /// Maps ingestion endpoints for snapshots and GitHub pull-based ingestion.
    /// </summary>
    /// <param name="app">The route builder used to register endpoints.</param>
    /// <returns>The same route builder instance for chaining.</returns>
    public static IEndpointRouteBuilder MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        // Manually ingest a snapshot payload.
        app.MapPost("/ingest/snapshot", async (
            IngestSnapshotRequest request,
            TenantContext tenantContext,
            YoteiDbContext db) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { errors = validationErrors });
            }

            var tenantId = tenantContext.TenantId;
            var repo = await db.Repositories
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Owner == request.Owner && r.Name == request.Name);

            if (repo is null)
            {
                repo = new Repository
                {
                    TenantId = tenantId,
                    Owner = request.Owner,
                    Name = request.Name,
                    DefaultBranch = request.DefaultBranch ?? "main"
                };
                db.Repositories.Add(repo);
            }
            else if (!string.IsNullOrWhiteSpace(request.DefaultBranch) && repo.DefaultBranch != request.DefaultBranch)
            {
                repo.DefaultBranch = request.DefaultBranch;
            }

            PullRequestSnapshot? existing = null;
            if (repo.Id != Guid.Empty)
            {
                existing = await db.PullRequestSnapshots.FirstOrDefaultAsync(snapshot =>
                    snapshot.RepositoryId == repo.Id &&
                    snapshot.PrNumber == request.PrNumber &&
                    snapshot.HeadSha == request.HeadSha);
            }

            if (existing is not null)
            {
                return Results.Ok(new { snapshotId = existing.Id, created = false });
            }

            var snapshot = new PullRequestSnapshot
            {
                TenantId = tenantId,
                Repository = repo,
                PrNumber = request.PrNumber,
                BaseSha = request.BaseSha,
                HeadSha = request.HeadSha,
                Source = request.Source ?? "fixture",
                Title = request.Title
            };

            db.PullRequestSnapshots.Add(snapshot);
            await db.SaveChangesAsync();

            return Results.Ok(new { snapshotId = snapshot.Id, created = true });
        });

        // Pull a GitHub PR into the ingestion pipeline.
        app.MapPost("/ingest/github", async (
            GitHubIngestRequest request,
            TenantContext tenantContext,
            IGithubIngestionService ingestionService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = request.Validate();
            if (validationErrors.Count > 0)
            {
                return Results.BadRequest(new { errors = validationErrors });
            }

            var result = await ingestionService.IngestPullRequestAsync(
                request,
                tenantContext.TenantId,
                null,
                cancellationToken);
            if (result.Errors.Count > 0 || result.SnapshotId is null)
            {
                return Results.Problem(detail: string.Join(" | ", result.Errors));
            }

            var response = new GitHubIngestResponse(result.SnapshotId.Value, result.Created, result.FileChangesCount);
            return Results.Ok(response);
        });

        // Sync configured GitHub repos for open PRs.
        app.MapPost("/ingest/github/sync", async (
            TenantContext tenantContext,
            IGithubIngestionService ingestionService,
            CancellationToken cancellationToken) =>
        {
            var result = await ingestionService.SyncConfiguredReposAsync(tenantContext.TenantId, cancellationToken);
            var response = new GitHubSyncResponse(result.Repositories, result.PullRequests, result.SnapshotsCreated, result.Errors);
            return Results.Ok(response);
        });

        // Handle GitHub webhook events to ingest PRs automatically.
        app.MapPost("/ingest/github/webhook", async (
            HttpRequest request,
            IGithubIngestionService ingestionService,
            TenantProvisioningService provisioningService,
            IOptions<FrontendSettings> frontendOptions,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("GitHubWebhook");
            var payload = await ReadRequestBodyAsync(request, cancellationToken);
            if (payload is null)
            {
                return Results.BadRequest(new { error = "Webhook payload is missing." });
            }

            var secret = configuration.GetValue<string>("GitHub:App:WebhookSecret");
            if (!GitHubWebhookVerifier.IsSignatureValid(secret, request.Headers["X-Hub-Signature-256"], payload))
            {
                return Results.Unauthorized();
            }

            var eventName = request.Headers["X-GitHub-Event"].ToString();
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return Results.BadRequest(new { error = "Missing X-GitHub-Event header." });
            }

            return eventName switch
            {
                "ping" => Results.Ok(new { status = "pong" }),
                "pull_request" => await HandlePullRequestWebhookAsync(
                    payload,
                    ingestionService,
                    provisioningService,
                    frontendOptions,
                    logger,
                    cancellationToken),
                _ => Results.Ok(new { ignored = eventName })
            };
        });

        return app;
    }

    // Reads the raw request body for webhook validation.
    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body is null)
        {
            return null;
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    // Handles GitHub pull request webhook events.
    private static async Task<IResult> HandlePullRequestWebhookAsync(
        string payload,
        IGithubIngestionService ingestionService,
        TenantProvisioningService provisioningService,
        IOptions<FrontendSettings> frontendOptions,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestPayload(
                payload,
                out var owner,
                out var name,
                out var prNumber,
                out var action,
                out var installationId))
        {
            return Results.BadRequest(new { error = "Invalid pull request payload." });
        }

        if (!ShouldIngestAction(action))
        {
            return Results.Ok(new { ignored = action });
        }

        var tenant = await provisioningService.EnsureTenantForInstallationAsync(installationId, cancellationToken);
        if (tenant is null)
        {
            return Results.Problem(detail: "Tenant provisioning failed for installation.");
        }

        var result = await ingestionService.IngestPullRequestAsync(
            new GitHubIngestRequest(owner, name, prNumber),
            tenant.Id,
            installationId,
            cancellationToken);

        if (result.Errors.Count > 0 || result.SnapshotId is null)
        {
            logger.LogWarning("GitHub webhook ingestion failed: {Errors}", string.Join(" | ", result.Errors));
            return Results.Problem(detail: string.Join(" | ", result.Errors));
        }

        if (ShouldPostPullRequestComment(action))
        {
            var commentBody = BuildYoteiPullRequestCommentBody(
                frontendOptions.Value,
                tenant,
                owner,
                name,
                prNumber,
                result.SnapshotId.Value);
            if (commentBody is null)
            {
                logger.LogWarning(
                    "Skipping GitHub comment because frontend URL or tenant token is missing for {Owner}/{Repo} PR #{PrNumber}.",
                    owner,
                    name,
                    prNumber);
            }
            else
            {
                var commentResult = await ingestionService.UpsertPullRequestCommentAsync(
                    new GitHubPullRequestCommentRequest(owner, name, prNumber, commentBody),
                    installationId,
                    cancellationToken);

                if (!commentResult)
                {
                    logger.LogWarning(
                        "GitHub comment failed for {Owner}/{Repo} PR #{PrNumber}.",
                        owner,
                        name,
                        prNumber);
                }
            }
        }

        return Results.Ok(new { snapshotId = result.SnapshotId, created = result.Created });
    }

    // Determines whether a pull request action should trigger ingestion.
    private static bool ShouldIngestAction(string? action)
    {
        return action switch
        {
            "opened" => true,
            "reopened" => true,
            "synchronize" => true,
            "ready_for_review" => true,
            _ => false
        };
    }

    // Determines whether a pull request webhook should post or update the Yotei comment.
    private static bool ShouldPostPullRequestComment(string? action)
    {
        return action?.ToLowerInvariant() switch
        {
            "opened" => true,
            "reopened" => true,
            "synchronize" => true,
            "ready_for_review" => true,
            _ => false
        };
    }

    // Builds the PR comment body that links to the Yotei frontend and displays the logo.
    private static string? BuildYoteiPullRequestCommentBody(
        FrontendSettings frontendSettings,
        Tenant tenant,
        string owner,
        string name,
        int prNumber,
        Guid snapshotId)
    {
        if (tenant is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(tenant.Token))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(frontendSettings.BaseUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (snapshotId == Guid.Empty)
        {
            return null;
        }

        if (!Uri.TryCreate(frontendSettings.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var baseUrl = baseUri.AbsoluteUri.TrimEnd('/');
        var dashboardUrl = QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>
        {
            ["tenant"] = tenant.Token,
            ["view"] = "dashboard",
            ["owner"] = owner,
            ["name"] = name,
            ["prNumber"] = prNumber.ToString()
        });
        var logoUrl = new Uri(baseUri, "yotei-logo.png").ToString();

        var lines = new[]
        {
            GitHubCommentMarkers.YoteiReviewLink,
            $"[![Yotei]({logoUrl})]({dashboardUrl})",
            string.Empty,
            $"View Yotei recommendations for PR #{prNumber}: {dashboardUrl}"
        };

        return string.Join(Environment.NewLine, lines);
    }

    // Parses pull request webhook payload details.
    private static bool TryParsePullRequestPayload(
        string payload,
        out string owner,
        out string name,
        out int prNumber,
        out string? action,
        out long installationId)
    {
        owner = string.Empty;
        name = string.Empty;
        prNumber = 0;
        action = null;
        installationId = 0;

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("action", out var actionElement))
        {
            return false;
        }

        action = actionElement.GetString();
        if (!root.TryGetProperty("pull_request", out var pullRequestElement))
        {
            return false;
        }

        if (!pullRequestElement.TryGetProperty("number", out var numberElement) ||
            !numberElement.TryGetInt32(out prNumber))
        {
            return false;
        }

        if (!root.TryGetProperty("repository", out var repoElement))
        {
            return false;
        }

        if (!TryGetString(repoElement, "name", out name))
        {
            return false;
        }

        if (!repoElement.TryGetProperty("owner", out var ownerElement) ||
            !TryGetString(ownerElement, "login", out owner))
        {
            return false;
        }

        if (!root.TryGetProperty("installation", out var installationElement))
        {
            return false;
        }

        if (!installationElement.TryGetProperty("id", out var idElement) ||
            !idElement.TryGetInt64(out installationId))
        {
            return false;
        }

        return true;
    }

    // Reads a required string property from a JSON element.
    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }
}
