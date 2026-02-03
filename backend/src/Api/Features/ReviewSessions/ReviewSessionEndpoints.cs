using System.Text;
using Microsoft.EntityFrameworkCore;
using Yotei.Api.Data;
using Yotei.Api.Infrastructure;
using Yotei.Api.Models;
using Yotei.Api.Storage;
using Yotei.Api.Features.Tenancy;

namespace Yotei.Api.Features.ReviewSessions;

public static class ReviewSessionEndpoints
{
    /// <summary>
    /// Maps review-session endpoints backed by snapshots.
    /// </summary>
    /// <param name="app">The route builder used to register endpoints.</param>
    /// <returns>The same route builder instance for chaining.</returns>
    public static IEndpointRouteBuilder MapReviewSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // List review sessions mapped from snapshots with pagination.
        app.MapGet("/review-sessions", async (
            int? limit,
            int? offset,
            TenantContext tenantContext,
            YoteiDbContext db) =>
        {
            var resolvedLimit = limit ?? 10;
            var resolvedOffset = offset ?? 0;
            var errors = new List<string>();

            if (resolvedLimit < 1 || resolvedLimit > 100)
            {
                errors.Add("limit must be between 1 and 100");
            }

            if (resolvedOffset < 0)
            {
                errors.Add("offset must be greater than or equal to 0");
            }

            if (errors.Count > 0)
            {
                return Results.BadRequest(new { errors });
            }

            var sessions = await db.PullRequestSnapshots
                .AsNoTracking()
                .Include(snapshot => snapshot.Repository)
                .Where(snapshot => snapshot.TenantId == tenantContext.TenantId)
                .OrderByDescending(snapshot => snapshot.IngestedAt)
                .Skip(resolvedOffset)
                .Take(resolvedLimit)
                .Select(snapshot => new ReviewSessionListItem(
                    snapshot.Id,
                    snapshot.Repository!.Owner,
                    snapshot.Repository.Name,
                    snapshot.PrNumber,
                    snapshot.BaseSha,
                    snapshot.HeadSha,
                    snapshot.Title,
                    snapshot.IngestedAt))
                .ToListAsync();

            return Results.Ok(sessions);
        });

        // Fetch a single review session backed by a snapshot id.
        app.MapGet("/review-sessions/{sessionId:guid}", async (
            Guid sessionId,
            TenantContext tenantContext,
            YoteiDbContext db) =>
        {
            var snapshot = await db.PullRequestSnapshots
                .AsNoTracking()
                .Include(s => s.Repository)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantContext.TenantId);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "review session not found" });
            }

            var response = new ReviewSessionDetail(
                snapshot.Id,
                snapshot.Repository!.Owner,
                snapshot.Repository.Name,
                snapshot.PrNumber,
                snapshot.BaseSha,
                snapshot.HeadSha,
                snapshot.Title,
                snapshot.Source,
                snapshot.Repository.DefaultBranch,
                snapshot.IngestedAt);

            return Results.Ok(response);
        });

        // Build or rebuild the review summary, tree, and explanations.
        app.MapPost("/review-sessions/{sessionId:guid}/build", async (
            Guid sessionId,
            TenantContext tenantContext,
            YoteiDbContext db,
            IRawDiffStorage storage,
            ReviewTreeBuilder builder,
            ReviewSessionLlmGenerator sessionLlmGenerator,
            IReviewExplanationGenerator explanationGenerator,
            ReviewNodeInsightsGenerator insightsGenerator,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await db.PullRequestSnapshots
                .Include(s => s.FileChanges)
                .Include(s => s.Repository)
                .FirstOrDefaultAsync(
                    s => s.Id == sessionId && s.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "review session not found" });
            }

            var existingSession = await db.ReviewSessions
                .Include(session => session.Nodes)
                .Include(session => session.Summary)
                .FirstOrDefaultAsync(
                    session => session.PullRequestSnapshotId == snapshot.Id &&
                        session.TenantId == tenantContext.TenantId,
                    cancellationToken);

            ReviewSession session;
            if (existingSession is null)
            {
                session = new ReviewSession
                {
                    Id = snapshot.Id,
                    TenantId = tenantContext.TenantId,
                    PullRequestSnapshotId = snapshot.Id
                };

                db.ReviewSessions.Add(session);
            }
            else
            {
                session = existingSession;

                if (session.TenantId == Guid.Empty)
                {
                    session.TenantId = tenantContext.TenantId;
                }

                if (session.Nodes.Count > 0)
                {
                    var nodeIds = session.Nodes.Select(node => node.Id).ToList();
                    var explanations = await db.ReviewNodeExplanations
                        .Where(explanation => nodeIds.Contains(explanation.ReviewNodeId))
                        .ToListAsync(cancellationToken);

                    var behaviourSummaries = await db.ReviewNodeBehaviourSummaries
                        .Where(summary => nodeIds.Contains(summary.ReviewNodeId))
                        .ToListAsync(cancellationToken);

                    var checklists = await db.ReviewNodeChecklists
                        .Where(checklist => nodeIds.Contains(checklist.ReviewNodeId))
                        .ToListAsync(cancellationToken);

                    var questions = await db.ReviewNodeQuestions
                        .Where(questionSet => nodeIds.Contains(questionSet.ReviewNodeId))
                        .ToListAsync(cancellationToken);

                    db.ReviewNodeExplanations.RemoveRange(explanations);
                    db.ReviewNodeBehaviourSummaries.RemoveRange(behaviourSummaries);
                    db.ReviewNodeChecklists.RemoveRange(checklists);
                    db.ReviewNodeQuestions.RemoveRange(questions);
                    db.ReviewNodes.RemoveRange(session.Nodes);
                }

                if (session.Summary is not null)
                {
                    db.ReviewSummaries.Remove(session.Summary);
                }
            }

            var buildResult = await builder.BuildAsync(session, snapshot, storage, cancellationToken);
            session.Summary = buildResult.Summary;

            var llmResult = await sessionLlmGenerator.GenerateAsync(
                snapshot,
                buildResult.Summary,
                buildResult.Nodes,
                cancellationToken);
            var overview = llmResult.Summary ?? ReviewSummaryFallbackBuilder.Build(snapshot, buildResult.Summary);
            buildResult.Summary.OverallSummary = overview.OverallSummary;
            buildResult.Summary.BeforeState = overview.BeforeState;
            buildResult.Summary.AfterState = overview.AfterState;

            db.ReviewSummaries.Add(buildResult.Summary);
            db.ReviewNodes.AddRange(buildResult.Nodes);
            await db.SaveChangesAsync(cancellationToken);

            var explanationsToStore = new List<ReviewNodeExplanation>();
            var summariesToStore = new List<ReviewNodeBehaviourSummary>();
            var checklistsToStore = new List<ReviewNodeChecklist>();
            var questionsToStore = new List<ReviewNodeQuestions>();
            foreach (var node in buildResult.Nodes)
            {
                var explanation = await explanationGenerator.GenerateAsync(node, cancellationToken);
                explanationsToStore.Add(explanation);

                if (node.NodeType == "file" && !string.IsNullOrWhiteSpace(node.Path))
                {
                    var change = snapshot.FileChanges
                        .FirstOrDefault(file => string.Equals(file.Path, node.Path, StringComparison.OrdinalIgnoreCase));

                    if (change is not null)
                    {
                        var checklist = insightsGenerator.BuildChecklist(node);
                        var summary = llmResult.BehaviourSummaries.TryGetValue(node.Id, out var llmSummary)
                            ? llmSummary
                            : insightsGenerator.BuildBehaviourSummary(node, change);
                        var questions = llmResult.Questions.TryGetValue(node.Id, out var llmQuestions)
                            ? llmQuestions
                            : BuildFallbackQuestions(node, checklist);
                        summariesToStore.Add(summary);
                        checklistsToStore.Add(checklist);
                        questionsToStore.Add(questions);
                    }
                }
            }

            db.ReviewNodeExplanations.AddRange(explanationsToStore);
            db.ReviewNodeBehaviourSummaries.AddRange(summariesToStore);
            db.ReviewNodeChecklists.AddRange(checklistsToStore);
            db.ReviewNodeQuestions.AddRange(questionsToStore);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new ReviewBuildResponse(session.Id, buildResult.Nodes.Count));
        });

        // Fetch the persisted review summary for a session.
        app.MapGet("/review-sessions/{sessionId:guid}/summary", async (
            Guid sessionId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var summary = await db.ReviewSummaries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ReviewSessionId == sessionId &&
                        item.ReviewSession != null &&
                        item.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (summary is null)
            {
                return Results.NotFound(new { error = "review summary not found" });
            }

            var response = new ReviewSummaryResponse(
                summary.ReviewSessionId,
                summary.ChangedFilesCount,
                summary.NewFilesCount,
                summary.ModifiedFilesCount,
                summary.DeletedFilesCount,
                summary.OverallSummary,
                summary.BeforeState,
                summary.AfterState,
                summary.EntryPoints,
                summary.SideEffects,
                summary.RiskTags,
                summary.TopPaths);

            return Results.Ok(response);
        });

        // Fetch the review tree for a session.
        app.MapGet("/review-sessions/{sessionId:guid}/change-tree", async (
            Guid sessionId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var session = await db.ReviewSessions
                .AsNoTracking()
                .Include(item => item.Nodes)
                .FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (session is null)
            {
                return Results.NotFound(new { error = "review tree not found" });
            }

            var nodes = session.Nodes
                .OrderBy(node => node.NodeType)
                .Select(node => new ReviewNodeResponse(
                    node.Id,
                    node.ParentId,
                    node.NodeType,
                    node.Label,
                    node.ChangeType,
                    node.RiskSeverity,
                    node.RiskTags,
                    node.Evidence,
                    node.Path))
                .ToList();

            var response = new ReviewTreeResponse(session.Id, session.CreatedAt, nodes);
            return Results.Ok(response);
        });

        // Fetch explanation for a review node.
        app.MapGet("/review-nodes/{nodeId:guid}/explanation", async (
            Guid nodeId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var explanation = await db.ReviewNodeExplanations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ReviewNodeId == nodeId &&
                        item.ReviewNode != null &&
                        item.ReviewNode.ReviewSession != null &&
                        item.ReviewNode.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (explanation is null)
            {
                return Results.NotFound(new { error = "review node explanation not found" });
            }

            var response = new ReviewNodeExplanationResponse(
                explanation.ReviewNodeId,
                explanation.Response,
                explanation.Source,
                explanation.CreatedAt);

            return Results.Ok(response);
        });

        // Fetch behaviour summary for a file review node.
        app.MapGet("/review-nodes/{nodeId:guid}/behaviour-summary", async (
            Guid nodeId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var summary = await db.ReviewNodeBehaviourSummaries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ReviewNodeId == nodeId &&
                        item.ReviewNode != null &&
                        item.ReviewNode.ReviewSession != null &&
                        item.ReviewNode.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (summary is null)
            {
                return Results.NotFound(new { error = "review node behaviour summary not found" });
            }

            var response = new ReviewNodeBehaviourSummaryResponse(
                summary.ReviewNodeId,
                summary.BehaviourChange,
                summary.Scope,
                summary.ReviewerFocus,
                summary.CreatedAt);

            return Results.Ok(response);
        });

        // Fetch checklist for a review node.
        app.MapGet("/review-nodes/{nodeId:guid}/checklist", async (
            Guid nodeId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var checklist = await db.ReviewNodeChecklists
                .AsNoTracking()
                .Include(item => item.ItemsDetailed)
                .FirstOrDefaultAsync(
                    item => item.ReviewNodeId == nodeId &&
                        item.ReviewNode != null &&
                        item.ReviewNode.ReviewSession != null &&
                        item.ReviewNode.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (checklist is null)
            {
                return Results.NotFound(new { error = "review node checklist not found" });
            }

            var items = checklist.ItemsDetailed.Count > 0
                ? checklist.ItemsDetailed
                    .OrderBy(item => item.CreatedAt)
                    .Select(item => new ReviewChecklistItemResponse(item.Text, item.Source, item.CreatedAt))
                    .ToList()
                : checklist.Items
                    .Select(item => new ReviewChecklistItemResponse(item, "heuristic", checklist.CreatedAt))
                    .ToList();

            var response = new ReviewNodeChecklistResponse(
                checklist.ReviewNodeId,
                items,
                checklist.CreatedAt);

            return Results.Ok(response);
        });

        // Append a checklist item sourced from conversation or automation.
        app.MapPost("/review-nodes/{nodeId:guid}/checklist/items", async (
            Guid nodeId,
            ReviewChecklistItemCreateRequest request,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { error = "request body is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(new { error = "text is required" });
            }

            var checklist = await db.ReviewNodeChecklists
                .Include(item => item.ItemsDetailed)
                .FirstOrDefaultAsync(
                    item => item.ReviewNodeId == nodeId &&
                        item.ReviewNode != null &&
                        item.ReviewNode.ReviewSession != null &&
                        item.ReviewNode.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (checklist is null)
            {
                return Results.NotFound(new { error = "review node checklist not found" });
            }

            var source = string.IsNullOrWhiteSpace(request.Source) ? "conversation" : request.Source.Trim();
            var normalizedText = request.Text.Trim();

            var alreadyExists = checklist.ItemsDetailed
                .Any(item => string.Equals(item.Text, normalizedText, StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
            {
                var item = new ReviewChecklistItem
                {
                    ReviewNodeChecklistId = checklist.Id,
                    Text = normalizedText,
                    Source = source,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                db.ReviewChecklistItems.Add(item);
            }

            await db.SaveChangesAsync(cancellationToken);

            var responseItems = await db.ReviewChecklistItems
                .AsNoTracking()
                .Where(item => item.ReviewNodeChecklistId == checklist.Id)
                .OrderBy(item => item.CreatedAt)
                .Select(item => new ReviewChecklistItemResponse(item.Text, item.Source, item.CreatedAt))
                .ToListAsync(cancellationToken);

            var response = new ReviewNodeChecklistResponse(
                checklist.ReviewNodeId,
                responseItems,
                checklist.CreatedAt);

            return Results.Ok(response);
        });

        // Submit a voice query scoped to a review node.
        app.MapPost("/review-nodes/{nodeId:guid}/voice-query", async (
            Guid nodeId,
            ReviewVoiceQueryRequest request,
            TenantContext tenantContext,
            YoteiDbContext db,
            ReviewVoiceQueryGenerator generator,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return Results.BadRequest(new { error = "request body is required" });
            }

            var question = string.IsNullOrWhiteSpace(request.Question)
                ? request.Transcript
                : request.Question;

            if (string.IsNullOrWhiteSpace(question))
            {
                return Results.BadRequest(new { error = "question or transcript is required" });
            }

            var node = await db.ReviewNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Id == nodeId && item.ReviewSession != null &&
                        item.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (node is null)
            {
                return Results.NotFound(new { error = "review node not found" });
            }

            var answer = generator.GenerateAnswer(node, question);
            var transcript = new ReviewTranscript
            {
                ReviewSessionId = node.ReviewSessionId,
                ReviewNodeId = node.Id,
                Question = question.Trim(),
                Answer = answer.Trim()
            };

            db.ReviewTranscripts.Add(transcript);
            await db.SaveChangesAsync(cancellationToken);

            var response = new ReviewVoiceQueryResponse(
                transcript.Id,
                transcript.ReviewSessionId,
                transcript.ReviewNodeId,
                transcript.Question,
                transcript.Answer,
                transcript.CreatedAt);

            return Results.Ok(response);
        });

        // Fetch transcript entries for a review session.
        app.MapGet("/review-sessions/{sessionId:guid}/transcript", async (
            Guid sessionId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var sessionExists = await db.ReviewSessions
                .AsNoTracking()
                .AnyAsync(
                    session => session.Id == sessionId && session.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (!sessionExists)
            {
                return Results.NotFound(new { error = "review session not found" });
            }

            var entries = await db.ReviewTranscripts
                .AsNoTracking()
                .Where(item => item.ReviewSessionId == sessionId)
                .OrderBy(item => item.CreatedAt)
                .Select(item => new ReviewTranscriptEntryResponse(
                    item.Id,
                    item.ReviewSessionId,
                    item.ReviewNodeId,
                    item.Question,
                    item.Answer,
                    item.CreatedAt))
                .ToListAsync(cancellationToken);

            var response = new ReviewTranscriptResponse(sessionId, entries);

            return Results.Ok(response);
        });

        // Fetch a compliance report for a review session.
        app.MapGet("/review-sessions/{sessionId:guid}/compliance-report", async (
            Guid sessionId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await db.PullRequestSnapshots
                .AsNoTracking()
                .Include(item => item.Repository)
                .FirstOrDefaultAsync(
                    item => item.Id == sessionId && item.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (snapshot is null)
            {
                return Results.NotFound(new { error = "review session not found" });
            }

            if (snapshot.Repository is null)
            {
                return Results.Problem(
                    detail: "repository not found for review session",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var summary = await db.ReviewSummaries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ReviewSessionId == sessionId &&
                        item.ReviewSession != null &&
                        item.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (summary is null)
            {
                return Results.NotFound(new { error = "review summary not found" });
            }

            var nodeIds = await db.ReviewNodes
                .AsNoTracking()
                .Where(node =>
                    node.ReviewSessionId == sessionId &&
                    node.ReviewSession != null &&
                    node.ReviewSession.TenantId == tenantContext.TenantId)
                .Select(node => node.Id)
                .ToListAsync(cancellationToken);

            var fileNodeCount = await db.ReviewNodes
                .AsNoTracking()
                .Where(node =>
                    node.ReviewSessionId == sessionId &&
                    node.NodeType == "file" &&
                    node.ReviewSession != null &&
                    node.ReviewSession.TenantId == tenantContext.TenantId)
                .CountAsync(cancellationToken);

            var checklists = nodeIds.Count > 0
                ? await db.ReviewNodeChecklists
                    .AsNoTracking()
                    .Include(checklist => checklist.ItemsDetailed)
                    .Where(checklist => nodeIds.Contains(checklist.ReviewNodeId))
                    .ToListAsync(cancellationToken)
                : [];

            var checklistItems = BuildComplianceChecklistItems(checklists);
            var checklistSummary = new ComplianceChecklistSummaryResponse(
                checklistItems.Count,
                checklistItems.Count(item => item.Source.Equals("heuristic", StringComparison.OrdinalIgnoreCase)),
                checklistItems.Count(item => item.Source.Equals("llm", StringComparison.OrdinalIgnoreCase)),
                checklistItems.Count(item => item.Source.Equals("conversation", StringComparison.OrdinalIgnoreCase)),
                fileNodeCount,
                checklistItems);

            var transcriptQuery = db.ReviewTranscripts
                .AsNoTracking()
                .Where(item => item.ReviewSessionId == sessionId);

            var transcriptCount = await transcriptQuery.CountAsync(cancellationToken);
            var transcriptHighlightsRaw = transcriptCount > 0
                ? await transcriptQuery
                    .OrderByDescending(item => item.CreatedAt)
                    .Take(5)
                    .ToListAsync(cancellationToken)
                : [];

            var lastEntryAt = transcriptHighlightsRaw.Count > 0
                ? transcriptHighlightsRaw.First().CreatedAt
                : (DateTimeOffset?)null;

            var response = new ComplianceReportResponse(
                sessionId,
                snapshot.Repository.Owner,
                snapshot.Repository.Name,
                snapshot.PrNumber,
                snapshot.Title,
                DateTimeOffset.UtcNow,
                new ComplianceSummaryResponse(
                    summary.ChangedFilesCount,
                    summary.NewFilesCount,
                    summary.ModifiedFilesCount,
                    summary.DeletedFilesCount,
                    summary.EntryPoints,
                    summary.SideEffects,
                    summary.TopPaths),
                summary.RiskTags,
                checklistSummary,
                new ComplianceTranscriptSummaryResponse(transcriptCount, lastEntryAt),
                BuildTranscriptHighlights(transcriptHighlightsRaw));

            return Results.Ok(response);
        });

        // Export transcript entries for a review session in JSON or CSV.
        app.MapGet("/review-sessions/{sessionId:guid}/transcript/export", async (
            Guid sessionId,
            string? format,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var resolvedFormat = string.IsNullOrWhiteSpace(format)
                ? "json"
                : format.Trim().ToLowerInvariant();

            if (resolvedFormat is not ("json" or "csv"))
            {
                return Results.BadRequest(new { error = "format must be json or csv" });
            }

            var sessionExists = await db.ReviewSessions
                .AsNoTracking()
                .AnyAsync(
                    session => session.Id == sessionId && session.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (!sessionExists)
            {
                return Results.NotFound(new { error = "review session not found" });
            }

            var entries = await db.ReviewTranscripts
                .AsNoTracking()
                .Where(item => item.ReviewSessionId == sessionId)
                .OrderBy(item => item.CreatedAt)
                .Select(item => new ReviewTranscriptEntryResponse(
                    item.Id,
                    item.ReviewSessionId,
                    item.ReviewNodeId,
                    item.Question,
                    item.Answer,
                    item.CreatedAt))
                .ToListAsync(cancellationToken);

            var response = new ReviewTranscriptResponse(sessionId, entries);

            if (resolvedFormat == "json")
            {
                return Results.Ok(response);
            }

            var csv = BuildTranscriptCsv(response);
            var payload = Encoding.UTF8.GetBytes(csv);
            return Results.File(payload, "text/csv", $"transcript-{sessionId}.csv");
        });

        // Fetch the reviewer questions for a node.
        app.MapGet("/review-nodes/{nodeId:guid}/questions", async (
            Guid nodeId,
            TenantContext tenantContext,
            YoteiDbContext db,
            CancellationToken cancellationToken) =>
        {
            var questions = await db.ReviewNodeQuestions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ReviewNodeId == nodeId &&
                        item.ReviewNode != null &&
                        item.ReviewNode.ReviewSession != null &&
                        item.ReviewNode.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (questions is null)
            {
                return Results.NotFound(new { error = "review questions not found" });
            }

            var response = new ReviewNodeQuestionsResponse(
                questions.ReviewNodeId,
                questions.Items,
                questions.Source,
                questions.CreatedAt);

            return Results.Ok(response);
        });

        // Fetch raw diff for a review node.
        app.MapGet("/review-nodes/{nodeId:guid}/diff", async (
            Guid nodeId,
            TenantContext tenantContext,
            YoteiDbContext db,
            IRawDiffStorage storage,
            CancellationToken cancellationToken) =>
        {
            var node = await db.ReviewNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Id == nodeId && item.ReviewSession != null &&
                        item.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (node is null)
            {
                return Results.NotFound(new { error = "review node not found" });
            }

            if (string.IsNullOrWhiteSpace(node.Path))
            {
                return Results.BadRequest(new { errors = new[] { "node does not have a file path" } });
            }

            var normalizedPath = node.Path.ToLower();

            var session = await db.ReviewSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.Id == node.ReviewSessionId && item.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (session is null)
            {
                return Results.NotFound(new { error = "review session not found" });
            }

            var fileChange = await db.FileChanges
                .FirstOrDefaultAsync(change =>
                    change.PullRequestSnapshotId == session.PullRequestSnapshotId &&
                    change.Path.ToLower() == normalizedPath,
                    cancellationToken);

            if (fileChange is null)
            {
                return Results.NotFound(new { error = "raw diff not found" });
            }

            if (!string.IsNullOrWhiteSpace(fileChange.RawDiffText))
            {
                return Results.Text(fileChange.RawDiffText, "text/plain");
            }

            if (string.IsNullOrWhiteSpace(fileChange.RawDiffRef))
            {
                return Results.NotFound(new { error = "raw diff not found" });
            }

            var diff = await storage.GetDiffAsync(fileChange.RawDiffRef, cancellationToken);
            if (diff is null)
            {
                return Results.NotFound(new { error = "raw diff not found" });
            }

            fileChange.RawDiffText = diff;
            db.FileChanges.Update(fileChange);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Text(diff, "text/plain");
        });

        // Regenerate explanation for a review node.
        app.MapPost("/review-nodes/{nodeId:guid}/explanation", async (
            Guid nodeId,
            TenantContext tenantContext,
            YoteiDbContext db,
            IReviewExplanationGenerator explanationGenerator,
            CancellationToken cancellationToken) =>
        {
            var node = await db.ReviewNodes
                .FirstOrDefaultAsync(
                    item => item.Id == nodeId && item.ReviewSession != null &&
                        item.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (node is null)
            {
                return Results.NotFound(new { error = "review node not found" });
            }

            var explanation = await explanationGenerator.GenerateAsync(node, cancellationToken);
            var existing = await db.ReviewNodeExplanations
                .FirstOrDefaultAsync(
                    item => item.ReviewNodeId == nodeId &&
                        item.ReviewNode != null &&
                        item.ReviewNode.ReviewSession != null &&
                        item.ReviewNode.ReviewSession.TenantId == tenantContext.TenantId,
                    cancellationToken);

            if (existing is null)
            {
                db.ReviewNodeExplanations.Add(explanation);
            }
            else
            {
                existing.Response = explanation.Response;
                existing.Source = explanation.Source;
                existing.CreatedAt = explanation.CreatedAt;
            }

            await db.SaveChangesAsync(cancellationToken);

            var response = new ReviewNodeExplanationResponse(
                nodeId,
                explanation.Response,
                explanation.Source,
                explanation.CreatedAt);

            return Results.Ok(response);
        });

        return app;
    }

    // Builds CSV content for transcript exports.
    private static string BuildTranscriptCsv(ReviewTranscriptResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TranscriptId,ReviewSessionId,ReviewNodeId,Question,Answer,CreatedAt");

        foreach (var entry in response.Entries)
        {
            builder
                .Append(EscapeCsvField(entry.Id.ToString()))
                .Append(',')
                .Append(EscapeCsvField(entry.ReviewSessionId.ToString()))
                .Append(',')
                .Append(EscapeCsvField(entry.ReviewNodeId.ToString()))
                .Append(',')
                .Append(EscapeCsvField(entry.Question))
                .Append(',')
                .Append(EscapeCsvField(entry.Answer))
                .Append(',')
                .Append(EscapeCsvField(entry.CreatedAt.ToString("O")))
                .AppendLine();
        }

        return builder.ToString();
    }

    // Escapes a CSV field to preserve commas and newlines.
    private static string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return escaped.IndexOfAny([',', '\n', '\r', '"']) >= 0
            ? $"\"{escaped}\""
            : escaped;
    }

    // Builds checklist items for compliance reports.
    private static List<ReviewChecklistItemResponse> BuildComplianceChecklistItems(
        IReadOnlyList<ReviewNodeChecklist> checklists)
    {
        var items = checklists.SelectMany(checklist => checklist.ItemsDetailed.Count switch
        {
            > 0 => checklist.ItemsDetailed.Select(item =>
                new ReviewChecklistItemResponse(item.Text, item.Source, item.CreatedAt)),
            _ => checklist.Items.Select(item =>
                new ReviewChecklistItemResponse(item, "heuristic", checklist.CreatedAt))
        });

        return items
            .OrderBy(item => item.CreatedAt)
            .ToList();
    }

    // Builds transcript highlights for compliance reports.
    private static List<ComplianceTranscriptExcerptResponse> BuildTranscriptHighlights(
        IReadOnlyList<ReviewTranscript> transcripts)
    {
        return transcripts
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ComplianceTranscriptExcerptResponse(
                item.Id,
                item.ReviewNodeId,
                item.Question,
                item.Answer,
                item.CreatedAt))
            .ToList();
    }

    // Builds heuristic questions from a checklist when LLM output is missing.
    private static ReviewNodeQuestions BuildFallbackQuestions(ReviewNode node, ReviewNodeChecklist checklist)
    {
        return new ReviewNodeQuestions
        {
            ReviewNodeId = node.Id,
            Items = checklist.Items,
            Source = "heuristic"
        };
    }
}
