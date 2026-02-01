using Microsoft.EntityFrameworkCore;
using Yotei.Api.Models;

namespace Yotei.Api.Data;

public class YoteiDbContext(DbContextOptions<YoteiDbContext> options) : DbContext(options)
{
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<IngestionCursor> IngestionCursors => Set<IngestionCursor>();
    public DbSet<PullRequestSnapshot> PullRequestSnapshots => Set<PullRequestSnapshot>();
    public DbSet<FileChange> FileChanges => Set<FileChange>();
    public DbSet<RawDiffBlob> RawDiffBlobs => Set<RawDiffBlob>();
    public DbSet<ChangeTree> ChangeTrees => Set<ChangeTree>();
    public DbSet<ChangeNode> ChangeNodes => Set<ChangeNode>();
    public DbSet<ChangeNodeExplanation> ChangeNodeExplanations => Set<ChangeNodeExplanation>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<ReviewSummary> ReviewSummaries => Set<ReviewSummary>();
    public DbSet<ReviewNode> ReviewNodes => Set<ReviewNode>();
    public DbSet<ReviewNodeExplanation> ReviewNodeExplanations => Set<ReviewNodeExplanation>();
    public DbSet<ReviewNodeBehaviourSummary> ReviewNodeBehaviourSummaries => Set<ReviewNodeBehaviourSummary>();
    public DbSet<ReviewNodeChecklist> ReviewNodeChecklists => Set<ReviewNodeChecklist>();
    public DbSet<ReviewChecklistItem> ReviewChecklistItems => Set<ReviewChecklistItem>();
    public DbSet<ReviewNodeQuestions> ReviewNodeQuestions => Set<ReviewNodeQuestions>();
    public DbSet<ReviewTranscript> ReviewTranscripts => Set<ReviewTranscript>();

    /// <summary>
    /// Configures entity indices and Postgres-specific column types.
    /// </summary>
    /// <param name="modelBuilder">The EF model builder used for configuration.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Snapshot-related indices.
        modelBuilder.Entity<Repository>()
            .HasIndex(repo => new { repo.Owner, repo.Name })
            .IsUnique();

        modelBuilder.Entity<IngestionCursor>()
            .HasIndex(cursor => cursor.RepositoryId)
            .IsUnique();

        modelBuilder.Entity<PullRequestSnapshot>()
            .HasIndex(snapshot => new { snapshot.RepositoryId, snapshot.PrNumber, snapshot.HeadSha })
            .IsUnique();

        modelBuilder.Entity<FileChange>()
            .HasIndex(change => new { change.PullRequestSnapshotId, change.Path });

        modelBuilder.Entity<RawDiffBlob>()
            .HasIndex(blob => new { blob.PullRequestSnapshotId, blob.Path })
            .IsUnique();

        modelBuilder.Entity<ChangeTree>()
            .HasIndex(tree => tree.PullRequestSnapshotId)
            .IsUnique();

        modelBuilder.Entity<ChangeNode>()
            .HasIndex(node => node.ChangeTreeId);

        // Review session indices and relationships.
        modelBuilder.Entity<ReviewSession>()
            .HasIndex(session => session.PullRequestSnapshotId)
            .IsUnique();

        modelBuilder.Entity<ReviewSummary>()
            .HasIndex(summary => summary.ReviewSessionId)
            .IsUnique();

        modelBuilder.Entity<ReviewNode>()
            .HasIndex(node => node.ReviewSessionId);

        modelBuilder.Entity<ReviewNode>()
            .HasIndex(node => node.ParentId);

        modelBuilder.Entity<ReviewNodeExplanation>()
            .HasIndex(explanation => explanation.ReviewNodeId)
            .IsUnique();

        modelBuilder.Entity<ReviewNodeBehaviourSummary>()
            .HasIndex(summary => summary.ReviewNodeId)
            .IsUnique();

        modelBuilder.Entity<ReviewNodeChecklist>()
            .HasIndex(checklist => checklist.ReviewNodeId)
            .IsUnique();

        modelBuilder.Entity<ReviewChecklistItem>()
            .HasIndex(item => item.ReviewNodeChecklistId);

        modelBuilder.Entity<ReviewNodeQuestions>()
            .HasIndex(questions => questions.ReviewNodeId)
            .IsUnique();

        modelBuilder.Entity<ReviewTranscript>()
            .HasIndex(transcript => transcript.ReviewSessionId);

        modelBuilder.Entity<ReviewTranscript>()
            .HasIndex(transcript => transcript.ReviewNodeId);

        // Store review summary arrays as text[] in Postgres.
        modelBuilder.Entity<ReviewSummary>()
            .Property(summary => summary.EntryPoints)
            .HasColumnType("text[]");

        modelBuilder.Entity<ReviewSummary>()
            .Property(summary => summary.SideEffects)
            .HasColumnType("text[]");

        modelBuilder.Entity<ReviewSummary>()
            .Property(summary => summary.RiskTags)
            .HasColumnType("text[]");

        modelBuilder.Entity<ReviewSummary>()
            .Property(summary => summary.TopPaths)
            .HasColumnType("text[]");

        // Store review node arrays as text[] in Postgres.
        modelBuilder.Entity<ReviewNode>()
            .Property(node => node.RiskTags)
            .HasColumnType("text[]");

        modelBuilder.Entity<ReviewNode>()
            .Property(node => node.Evidence)
            .HasColumnType("text[]");

        modelBuilder.Entity<ReviewNodeChecklist>()
            .Property(checklist => checklist.Items)
            .HasColumnType("text[]");

        modelBuilder.Entity<ReviewChecklistItem>()
            .HasOne(item => item.ReviewNodeChecklist)
            .WithMany(checklist => checklist.ItemsDetailed)
            .HasForeignKey(item => item.ReviewNodeChecklistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReviewNodeQuestions>()
            .Property(questions => questions.Items)
            .HasColumnType("text[]");
    }
}
