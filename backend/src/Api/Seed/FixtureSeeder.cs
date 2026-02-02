using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Seed;

/// <summary>
/// Seeds the API with fixture data for local development.
/// </summary>
public static class FixtureSeeder
{
    /// <summary>
    /// Loads fixture data from disk and seeds it into the database.
    /// </summary>
    public static async Task SeedAsync(
        YoteiDbContext db,
        IRawDiffStorage storage,
        string contentRoot,
        SeedSettings settings,
        ILogger logger)
    {
        if (!settings.Enabled)
        {
            return;
        }

        var fixturePath = settings.FixturePath;
        if (string.IsNullOrWhiteSpace(fixturePath))
        {
            fixturePath = Path.Combine(contentRoot, "Fixtures", "seed.json");
        }
        else if (!Path.IsPathRooted(fixturePath))
        {
            fixturePath = Path.Combine(contentRoot, fixturePath);
        }

        if (!File.Exists(fixturePath))
        {
            logger.LogWarning("Seed fixture not found at {FixturePath}", fixturePath);
            return;
        }

        var json = await File.ReadAllTextAsync(fixturePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var fixtures = ParseFixtures(json, options, logger, fixturePath);
        if (fixtures.Count == 0)
        {
            logger.LogWarning("Seed fixture at {FixturePath} could not be parsed.", fixturePath);
            return;
        }

        foreach (var fixture in fixtures)
        {
            await SeedFixtureAsync(db, storage, fixture, logger);
        }

        logger.LogInformation("Seed fixtures ingested from {FixturePath}", fixturePath);
    }

    /// <summary>
    /// Parses fixture JSON, supporting both a single fixture and a fixtures array.
    /// </summary>
    private static List<SeedFixture> ParseFixtures(
        string json,
        JsonSerializerOptions options,
        ILogger logger,
        string fixturePath)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("fixtures", out var fixturesElement) &&
                fixturesElement.ValueKind == JsonValueKind.Array)
            {
                var fixtures = JsonSerializer.Deserialize<List<SeedFixture>>(fixturesElement.GetRawText(), options);
                return fixtures ?? [];
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Seed fixture at {FixturePath} could not be parsed.", fixturePath);
            return [];
        }

        var singleFixture = JsonSerializer.Deserialize<SeedFixture>(json, options);
        return singleFixture is null ? [] : [singleFixture];
    }

    /// <summary>
    /// Seeds a single fixture, ensuring the snapshot exists before storing raw diffs.
    /// </summary>
    private static async Task SeedFixtureAsync(
        YoteiDbContext db,
        IRawDiffStorage storage,
        SeedFixture fixture,
        ILogger logger)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync();
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name = "Fixture",
                Slug = "fixture",
                Token = "fixture-token"
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        var repo = await db.Repositories
            .FirstOrDefaultAsync(r => r.TenantId == tenant.Id && r.Owner == fixture.Owner && r.Name == fixture.Name);
        if (repo is null)
        {
            repo = new Repository
            {
                TenantId = tenant.Id,
                Owner = fixture.Owner,
                Name = fixture.Name,
                DefaultBranch = fixture.DefaultBranch ?? "main"
            };
            db.Repositories.Add(repo);
        }
        else if (!string.IsNullOrWhiteSpace(fixture.DefaultBranch))
        {
            repo.DefaultBranch = fixture.DefaultBranch!;
        }

        if (repo.Id != Guid.Empty)
        {
            var existingSnapshot = await db.PullRequestSnapshots
                .Include(snapshot => snapshot.FileChanges)
                .FirstOrDefaultAsync(snapshot =>
                    snapshot.RepositoryId == repo.Id &&
                    snapshot.TenantId == tenant.Id &&
                    snapshot.PrNumber == fixture.PrNumber &&
                    snapshot.HeadSha == fixture.HeadSha);

            if (existingSnapshot is not null)
            {
                logger.LogInformation("Seed snapshot already present for {Owner}/{Name} PR {PrNumber}.",
                    fixture.Owner,
                    fixture.Name,
                    fixture.PrNumber);
                return;
            }
        }

        var snapshot = new PullRequestSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Repository = repo,
            PrNumber = fixture.PrNumber,
            BaseSha = fixture.BaseSha,
            HeadSha = fixture.HeadSha,
            Source = fixture.Source ?? "fixture",
            Title = fixture.Title,
            IngestedAt = fixture.IngestedAt ?? DateTimeOffset.UtcNow
        };

        db.PullRequestSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        if (fixture.FileChanges is null)
        {
            return;
        }

        foreach (var change in fixture.FileChanges)
        {
            var rawDiffRef = change.RawDiffRef;
            if (!string.IsNullOrWhiteSpace(change.RawDiff))
            {
                rawDiffRef = await storage.StoreDiffAsync(snapshot.Id, change.Path, change.RawDiff, CancellationToken.None);
            }

            snapshot.FileChanges.Add(new FileChange
            {
                Path = change.Path,
                ChangeType = change.ChangeType ?? "modified",
                AddedLines = change.AddedLines,
                DeletedLines = change.DeletedLines,
                RawDiffRef = rawDiffRef,
                RawDiffText = change.RawDiff
            });
        }

        await db.SaveChangesAsync();
    }
}

/// <summary>
/// Configuration for enabling fixture seeding and choosing a fixture path.
/// </summary>
public record SeedSettings(bool Enabled, string? FixturePath);

/// <summary>
/// Represents a single snapshot fixture to seed.
/// </summary>
public record SeedFixture(
    string Owner,
    string Name,
    int PrNumber,
    string BaseSha,
    string HeadSha,
    string? DefaultBranch,
    string? Title,
    string? Source,
    DateTimeOffset? IngestedAt,
    List<SeedFileChange>? FileChanges);

/// <summary>
/// Represents a file change to seed for a snapshot.
/// </summary>
public record SeedFileChange(
    string Path,
    string? ChangeType,
    int AddedLines,
    int DeletedLines,
    string? RawDiffRef,
    string? RawDiff);
