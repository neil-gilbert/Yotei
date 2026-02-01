namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Summarizes a review session for list views.
/// </summary>
/// <example>
/// <code>
/// {
///   "id": "00000000-0000-0000-0000-000000000000",
///   "owner": "octo",
///   "name": "repo",
///   "prNumber": 12,
///   "baseSha": "abc",
///   "headSha": "def",
///   "title": "Update billing flow",
///   "ingestedAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewSessionListItem(
    Guid Id,
    string Owner,
    string Name,
    int PrNumber,
    string BaseSha,
    string HeadSha,
    string? Title,
    DateTimeOffset IngestedAt);

/// <summary>
/// Provides detail about a single review session.
/// </summary>
/// <example>
/// <code>
/// {
///   "id": "00000000-0000-0000-0000-000000000000",
///   "owner": "octo",
///   "name": "repo",
///   "prNumber": 12,
///   "baseSha": "abc",
///   "headSha": "def",
///   "title": "Update billing flow",
///   "source": "fixture",
///   "defaultBranch": "main",
///   "ingestedAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewSessionDetail(
    Guid Id,
    string Owner,
    string Name,
    int PrNumber,
    string BaseSha,
    string HeadSha,
    string? Title,
    string Source,
    string DefaultBranch,
    DateTimeOffset IngestedAt);

/// <summary>
/// Represents the persisted summary for a review session.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewSessionId": "00000000-0000-0000-0000-000000000000",
///   "changedFilesCount": 4,
///   "newFilesCount": 1,
///   "modifiedFilesCount": 2,
///   "deletedFilesCount": 1,
///   "overallSummary": "Updates checkout retries and API gateways.",
///   "beforeState": "Previously, the retry flow relied on legacy queue handlers.",
///   "afterState": "Now, retries are centralized behind a new billing gateway.",
///   "entryPoints": ["src/api/controller"],
///   "sideEffects": ["db", "network"],
///   "riskTags": ["auth", "data"],
///   "topPaths": ["src/api/controller"]
/// }
/// </code>
/// </example>
public record ReviewSummaryResponse(
    Guid ReviewSessionId,
    int ChangedFilesCount,
    int NewFilesCount,
    int ModifiedFilesCount,
    int DeletedFilesCount,
    string OverallSummary,
    string BeforeState,
    string AfterState,
    IReadOnlyList<string> EntryPoints,
    IReadOnlyList<string> SideEffects,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> TopPaths);

/// <summary>
/// Represents a node in the review comprehension tree.
/// </summary>
/// <example>
/// <code>
/// {
///   "id": "00000000-0000-0000-0000-000000000000",
///   "parentId": null,
///   "nodeType": "group",
///   "label": "Overview",
///   "changeType": "modified",
///   "riskSeverity": "low",
///   "riskTags": [],
///   "evidence": []
/// }
/// </code>
/// </example>
public record ReviewNodeResponse(
    Guid Id,
    Guid? ParentId,
    string NodeType,
    string Label,
    string ChangeType,
    string RiskSeverity,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> Evidence,
    string? Path);

/// <summary>
/// Wraps the tree of review nodes for a session.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewSessionId": "00000000-0000-0000-0000-000000000000",
///   "createdAt": "2025-01-01T00:00:00Z",
///   "nodes": []
/// }
/// </code>
/// </example>
public record ReviewTreeResponse(
    Guid ReviewSessionId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReviewNodeResponse> Nodes);

/// <summary>
/// Reports the outcome of a review build operation.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewSessionId": "00000000-0000-0000-0000-000000000000",
///   "nodeCount": 12
/// }
/// </code>
/// </example>
public record ReviewBuildResponse(Guid ReviewSessionId, int NodeCount);

/// <summary>
/// Provides the explanation for a review node.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewNodeId": "00000000-0000-0000-0000-000000000000",
///   "response": "Change focus: ...",
///   "source": "heuristic",
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewNodeExplanationResponse(
    Guid ReviewNodeId,
    string Response,
    string Source,
    DateTimeOffset CreatedAt);

/// <summary>
/// Provides the behavior summary for a review node.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewNodeId": "00000000-0000-0000-0000-000000000000",
///   "behaviourChange": "Changes payment logic.",
///   "scope": "Scope: API entry point (heuristic, high confidence).",
///   "reviewerFocus": "Reviewer focus: ...",
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewNodeBehaviourSummaryResponse(
    Guid ReviewNodeId,
    string BehaviourChange,
    string Scope,
    string ReviewerFocus,
    DateTimeOffset CreatedAt);

/// <summary>
/// Provides the review checklist for a review node.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewNodeId": "00000000-0000-0000-0000-000000000000",
///   "items": ["Question 1", "Question 2"],
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewNodeChecklistResponse(
    Guid ReviewNodeId,
    IReadOnlyList<ReviewChecklistItemResponse> Items,
    DateTimeOffset CreatedAt);

/// <summary>
/// Represents a checklist item with source metadata.
/// </summary>
/// <example>
/// <code>
/// {
///   "text": "Are retries safe and idempotent?",
///   "source": "heuristic",
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewChecklistItemResponse(
    string Text,
    string Source,
    DateTimeOffset CreatedAt);

/// <summary>
/// Represents a request to add a checklist item.
/// </summary>
/// <example>
/// <code>
/// {
///   "text": "Verify new webhook retry behavior.",
///   "source": "conversation"
/// }
/// </code>
/// </example>
public record ReviewChecklistItemCreateRequest(
    string? Text,
    string? Source);

/// <summary>
/// Provides the reviewer questions for a review node.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewNodeId": "00000000-0000-0000-0000-000000000000",
///   "items": ["What happens when payment retries fail?"],
///   "source": "llm",
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewNodeQuestionsResponse(
    Guid ReviewNodeId,
    IReadOnlyList<string> Items,
    string Source,
    DateTimeOffset CreatedAt);

/// <summary>
/// Represents a voice query request for a review node.
/// </summary>
/// <example>
/// <code>
/// {
///   "question": "What is the risk here?",
///   "transcript": "What is the risk here?"
/// }
/// </code>
/// </example>
public record ReviewVoiceQueryRequest(
    string? Question,
    string? Transcript);

/// <summary>
/// Represents the response for a voice query.
/// </summary>
/// <example>
/// <code>
/// {
///   "transcriptId": "00000000-0000-0000-0000-000000000000",
///   "reviewSessionId": "00000000-0000-0000-0000-000000000000",
///   "reviewNodeId": "00000000-0000-0000-0000-000000000000",
///   "question": "What is the risk here?",
///   "answer": "For file src/api/payments.cs ...",
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewVoiceQueryResponse(
    Guid TranscriptId,
    Guid ReviewSessionId,
    Guid ReviewNodeId,
    string Question,
    string Answer,
    DateTimeOffset CreatedAt);

/// <summary>
/// Represents a transcript entry for a review session.
/// </summary>
/// <example>
/// <code>
/// {
///   "id": "00000000-0000-0000-0000-000000000000",
///   "reviewSessionId": "00000000-0000-0000-0000-000000000000",
///   "reviewNodeId": "00000000-0000-0000-0000-000000000000",
///   "question": "What changed?",
///   "answer": "For file src/api/payments.cs ...",
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ReviewTranscriptEntryResponse(
    Guid Id,
    Guid ReviewSessionId,
    Guid ReviewNodeId,
    string Question,
    string Answer,
    DateTimeOffset CreatedAt);

/// <summary>
/// Represents the transcript list for a review session.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewSessionId": "00000000-0000-0000-0000-000000000000",
///   "entries": []
/// }
/// </code>
/// </example>
public record ReviewTranscriptResponse(
    Guid ReviewSessionId,
    IReadOnlyList<ReviewTranscriptEntryResponse> Entries);

/// <summary>
/// Represents the compliance report for a review session.
/// </summary>
/// <example>
/// <code>
/// {
///   "reviewSessionId": "00000000-0000-0000-0000-000000000000",
///   "owner": "octo",
///   "name": "repo",
///   "prNumber": 42,
///   "title": "Update billing flow",
///   "generatedAt": "2025-01-01T00:00:00Z",
///   "summary": {},
///   "riskTags": ["money"],
///   "checklist": {},
///   "transcript": {},
///   "transcriptHighlights": []
/// }
/// </code>
/// </example>
public record ComplianceReportResponse(
    Guid ReviewSessionId,
    string Owner,
    string Name,
    int PrNumber,
    string? Title,
    DateTimeOffset GeneratedAt,
    ComplianceSummaryResponse Summary,
    IReadOnlyList<string> RiskTags,
    ComplianceChecklistSummaryResponse Checklist,
    ComplianceTranscriptSummaryResponse Transcript,
    IReadOnlyList<ComplianceTranscriptExcerptResponse> TranscriptHighlights);

/// <summary>
/// Represents the summary portion of a compliance report.
/// </summary>
/// <example>
/// <code>
/// {
///   "changedFilesCount": 4,
///   "newFilesCount": 1,
///   "modifiedFilesCount": 2,
///   "deletedFilesCount": 1,
///   "entryPoints": ["src/api/controller"],
///   "sideEffects": ["db"],
///   "topPaths": ["src/api/controller"]
/// }
/// </code>
/// </example>
public record ComplianceSummaryResponse(
    int ChangedFilesCount,
    int NewFilesCount,
    int ModifiedFilesCount,
    int DeletedFilesCount,
    IReadOnlyList<string> EntryPoints,
    IReadOnlyList<string> SideEffects,
    IReadOnlyList<string> TopPaths);

/// <summary>
/// Represents checklist coverage in a compliance report.
/// </summary>
/// <example>
/// <code>
/// {
///   "totalItems": 4,
///   "heuristicItems": 2,
///   "llmItems": 1,
///   "conversationItems": 1,
///   "fileNodeCount": 2,
///   "items": []
/// }
/// </code>
/// </example>
public record ComplianceChecklistSummaryResponse(
    int TotalItems,
    int HeuristicItems,
    int LlmItems,
    int ConversationItems,
    int FileNodeCount,
    IReadOnlyList<ReviewChecklistItemResponse> Items);

/// <summary>
/// Represents transcript coverage in a compliance report.
/// </summary>
/// <example>
/// <code>
/// {
///   "totalEntries": 3,
///   "lastEntryAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ComplianceTranscriptSummaryResponse(
    int TotalEntries,
    DateTimeOffset? LastEntryAt);

/// <summary>
/// Represents a transcript highlight in a compliance report.
/// </summary>
/// <example>
/// <code>
/// {
///   "transcriptId": "00000000-0000-0000-0000-000000000000",
///   "reviewNodeId": "00000000-0000-0000-0000-000000000000",
///   "question": "What changed?",
///   "answer": "For file src/api/payments.cs ...",
///   "createdAt": "2025-01-01T00:00:00Z"
/// }
/// </code>
/// </example>
public record ComplianceTranscriptExcerptResponse(
    Guid TranscriptId,
    Guid ReviewNodeId,
    string Question,
    string Answer,
    DateTimeOffset CreatedAt);
