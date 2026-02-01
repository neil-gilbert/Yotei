using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Models;

namespace Yotei.Api.Storage;

/// <summary>
/// Stores raw diffs in the database using EF Core to keep local development deterministic.
/// </summary>
public sealed class DatabaseRawDiffStorage : IRawDiffStorage
{
    private const string ReferencePrefix = "db://";
    private readonly YoteiDbContext _dbContext;

    /// <summary>
    /// Initializes the storage with the current database context.
    /// </summary>
    /// <param name="dbContext">The EF Core context used to persist raw diff blobs.</param>
    public DatabaseRawDiffStorage(YoteiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Stores a raw diff blob and returns a database reference for later retrieval.
    /// </summary>
    /// <param name="snapshotId">The snapshot identifier that owns the diff.</param>
    /// <param name="path">The normalized file path for the diff.</param>
    /// <param name="diff">The raw diff text.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A database reference that can be resolved via <see cref="GetDiffAsync"/>.</returns>
    public async Task<string> StoreDiffAsync(
        Guid snapshotId,
        string path,
        string diff,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path is required.", nameof(path));
        }

        if (diff is null)
        {
            throw new ArgumentNullException(nameof(diff));
        }

        var normalizedPath = NormalizePath(path);

        var existing = await _dbContext.RawDiffBlobs
            .FirstOrDefaultAsync(
                blob => blob.PullRequestSnapshotId == snapshotId && blob.Path == normalizedPath,
                cancellationToken);

        if (existing is null)
        {
            existing = new RawDiffBlob
            {
                PullRequestSnapshotId = snapshotId,
                Path = normalizedPath,
                DiffText = diff
            };

            _dbContext.RawDiffBlobs.Add(existing);
        }
        else
        {
            existing.DiffText = diff;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return $"{ReferencePrefix}{existing.Id}";
    }

    /// <summary>
    /// Resolves a database raw diff reference into its stored diff text.
    /// </summary>
    /// <param name="rawDiffRef">The stored reference returned by <see cref="StoreDiffAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The diff text, or null when the reference cannot be resolved.</returns>
    public async Task<string?> GetDiffAsync(string rawDiffRef, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawDiffRef))
        {
            return null;
        }

        if (!TryParseReference(rawDiffRef, out var diffId))
        {
            return null;
        }

        var blob = await _dbContext.RawDiffBlobs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == diffId, cancellationToken);

        return blob?.DiffText;
    }

    // Normalizes the diff path to avoid duplicate keys for equivalent file paths.
    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    // Parses the db:// reference format into a Guid value.
    private static bool TryParseReference(string rawDiffRef, out Guid diffId)
    {
        diffId = Guid.Empty;

        if (!rawDiffRef.StartsWith(ReferencePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = rawDiffRef[ReferencePrefix.Length..];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Guid.TryParse(value, out diffId);
    }
}
