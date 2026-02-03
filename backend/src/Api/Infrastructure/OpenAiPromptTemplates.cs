namespace Yotei.Api.Infrastructure;

/// <summary>
/// Text templates and schema descriptions used when building OpenAI requests.
/// Prompts are designed to produce AI-native review artifacts:
/// - Behaviour-first explanations (not metadata)
/// - Evidence-grounded claims (tokens from diff)
/// - Structured JSON outputs that drive the UI (checklist/graph/highlights)
/// </summary>
public sealed record OpenAiPromptTemplates
{
    // ---------------------------
    // System prompts (strict, no roleplay trope)
    // ---------------------------

    /// <summary>
    /// System prompt for node/file-level review analysis.
    /// Produces behaviour summary, execution paths, side effects, risk tags,
    /// reviewer questions, test suggestions, and unknowns.
    /// </summary>
    public string NodeReviewSystemPrompt { get; init; } =
        """
You are producing a review artifact for a human code reviewer.

Goal:
- Help the reviewer understand the behavioural impact of this change quickly.

Hard rules:
- Use ONLY the provided diff/context. Do not invent requirements or system behaviour.
- No generic filler (e.g., "ensure it works", "review for correctness", "file was modified").
- Every important claim MUST include short evidence tokens that appear in the diff (identifiers/strings).
- If something cannot be inferred, list it explicitly under "unknowns".
- Return JSON ONLY that matches the provided schema. No markdown.
""";

    /// <summary>
    /// System prompt for legacy behaviour summary generation.
    /// </summary>
    public string BehaviourSummarySystemPrompt { get; init; } =
        """
You are producing a concise behaviour summary for a code reviewer.

Hard rules:
- Use ONLY the provided diff/context.
- Keep the response concise and specific to the diff.
- Return JSON ONLY that matches the provided schema. No markdown.
""";

    /// <summary>
    /// System prompt for reviewer question generation (if you still run it separately).
    /// Prefer using NodeReview where possible.
    /// </summary>
    public string ReviewerQuestionsSystemPrompt { get; init; } =
        """
You are producing targeted review questions for a human reviewer.

Hard rules:
- Use ONLY the provided diff/context.
- Questions must be specific and actionable (e.g., idempotency under retry, timeout handling),
  not generic (e.g., "check edge cases").
- If you cannot justify a question from evidence, do not include it.
- Return JSON ONLY that matches the provided schema. No markdown.
""";

    /// <summary>
    /// System prompt for PR/session-level review summary.
    /// This summary is reviewer-oriented and should mention behavioural deltas and focus areas.
    /// </summary>
    public string ReviewSummarySystemPrompt { get; init; } =
        """
You are producing an overview review artifact for a human reviewer.

Hard rules:
- Use ONLY the provided context/diff excerpts.
- Focus on behavioural change, execution impact, and reviewer focus areas.
- No file lists unless they serve a behavioural point.
- Call out unknowns explicitly.
- Return JSON ONLY that matches the provided schema. No markdown.
""";

    /// <summary>
    /// System prompt for combined review session output (summary + per-file review content).
    /// </summary>
    public string ReviewSessionSystemPrompt { get; init; } =
        """
You are producing a single review bundle for a human reviewer.

Hard rules:
- Use ONLY the provided context/diff excerpts.
- Use the provided node ids and paths exactly as given.
- Focus on behavioural change, execution impact, and reviewer focus areas.
- Return JSON ONLY that matches the provided schema. No markdown.
""";

    /// <summary>
    /// System prompt for execution-flow graph inference for the UI (routes/flows affected).
    /// Produces nodes/edges with confidence + evidence, suitable for animation/highlighting.
    /// </summary>
    public string FlowGraphSystemPrompt { get; init; } =
        """
You are producing an execution-impact graph for a human reviewer.

Goal:
- Infer affected entry points and runtime flows from the provided context.

Hard rules:
- Language-agnostic. Do not assume frameworks.
- Use ONLY provided context/diffs. Do not invent endpoints/services.
- Every node and edge MUST include evidence tokens from the diff/metadata.
- If inference is weak, set confidence to low and explain in evidence.
- Return JSON ONLY that matches the provided schema. No markdown.
""";

    /// <summary>
    /// System prompt for conversational review turns (text or voice).
    /// Must be scoped to selected node/flow and may propose structured UI updates.
    /// </summary>
    public string ConversationTurnSystemPrompt { get; init; } =
        """
You are answering a reviewer question within an active review session.

Goal:
- Provide a short, speakable answer and propose structured UI updates when justified.

Hard rules:
- Stay scoped to the provided selected node/flow context.
- Use ONLY provided evidence (diff hunks, analysis JSON, flow graph).
- If the question is out of scope, say what information is missing.
- Return JSON ONLY matching the provided schema. No markdown.
""";

    // ---------------------------
    // User prompt templates
    // ---------------------------

    /// <summary>
    /// Template for node/file-level prompt body used in NodeReview.
    /// Keep Diff as ground truth. "Signals" can be your heuristics (keywords, etc).
    /// </summary>
    public string NodeReviewPromptTemplate { get; init; } =
        """
Repository: {Repository}
PR: #{PrNumber}
Title: {Title}

Node type: {NodeType}          (file | hunk | change_unit)
Node label: {NodeLabel}        (e.g. src/api/payments.cs)
Change type: {ChangeType}      (added | modified | deleted)

Heuristic risk tags: {RiskTags}
Detected signals: {Signals}

Raw diff (ground truth):
{Diff}
""";

    /// <summary>
    /// Back-compat template for file-level prompts (older behaviour summary + questions).
    /// Prefer NodeReviewPromptTemplate for new calls.
    /// </summary>
    public string FilePromptTemplate { get; init; } =
        """
File path: {FilePath}
Change type: {ChangeType}
Risk tags: {RiskTags}
Evidence: {Evidence}
Diff:
{Diff}
""";

    /// <summary>
    /// Template for the review summary prompt header.
    /// Keep it behavioural: entry points/side effects/risks + diff excerpts.
    /// </summary>
    public string ReviewSummaryHeaderTemplate { get; init; } =
        """
Repository: {Repository}
PR: #{PrNumber}
Title: {Title}
Files changed: {ChangedFilesCount} (new {NewFilesCount}, modified {ModifiedFilesCount}, deleted {DeletedFilesCount})
Entry points (heuristic): {EntryPoints}
Side effects (heuristic): {SideEffects}
Risk tags (heuristic): {RiskTags}
Test files: {TestFiles}

Diff excerpts (ground truth):
""";

    public string ReviewSummaryFileTemplate { get; init; } = "File: {Path} ({ChangeType}, +{AddedLines}/-{DeletedLines})";
    public string ReviewSummaryDiffLabel { get; init; } = "Diff:";
    public string ReviewSummarySeparator { get; init; } = "---";

    /// <summary>
    /// Template for the review session prompt header.
    /// </summary>
    public string ReviewSessionHeaderTemplate { get; init; } =
        """
Repository: {Repository}
PR: #{PrNumber}
Title: {Title}
Files changed: {ChangedFilesCount} (new {NewFilesCount}, modified {ModifiedFilesCount}, deleted {DeletedFilesCount})
Entry points (heuristic): {EntryPoints}
Side effects (heuristic): {SideEffects}
Risk tags (heuristic): {RiskTags}
Top paths (heuristic): {TopPaths}
Test files: {TestFiles}

Files:
""";

    /// <summary>
    /// Template for each file block in the review session prompt.
    /// </summary>
    public string ReviewSessionFileTemplate { get; init; } =
        """
File {Index}:
NodeId: {NodeId}
Path: {Path}
Change type: {ChangeType}
Added/Deleted: +{AddedLines}/-{DeletedLines}
Risk tags: {RiskTags}
Evidence: {Evidence}
""";

    public string ReviewSessionDiffLabel { get; init; } = "Diff:";
    public string ReviewSessionSeparator { get; init; } = "---";

    /// <summary>
    /// Template for PR/session-level flow graph inference.
    /// Provide compact summaries + key diff excerpts (not full repo).
    /// </summary>
    public string FlowGraphPromptTemplate { get; init; } =
        """
Repository: {Repository}
PR: #{PrNumber}
Title: {Title}
Description: {Description}

File summaries (heuristic):
{FileSummaries}

Key diff excerpts (ground truth):
{DiffExcerpts}
""";

    /// <summary>
    /// Template for a single conversational turn (voice/text).
    /// Provide selected context + relevant evidence.
    /// </summary>
    public string ConversationTurnPromptTemplate { get; init; } =
        """
Repository: {Repository}
PR: #{PrNumber}
Title: {Title}
Mode: {Mode} (text | voice)

Selected context:
- Selected node id: {SelectedNodeId}
- Selected node label: {SelectedNodeLabel}
- Selected flow id (if any): {SelectedFlowId}

Known analysis for this context:
Node review JSON (if any):
{NodeReviewJson}

Flow graph JSON (if any):
{FlowGraphJson}

Relevant diff excerpts (ground truth):
{RelevantDiffExcerpts}

Reviewer question:
{ReviewerQuestion}
""";

    // ---------------------------
    // Fallbacks
    // ---------------------------

    public string NoneValue { get; init; } = "none";
    public string UntitledValue { get; init; } = "Untitled";
    public string DiffUnavailableValue { get; init; } = "(diff unavailable)";

    /// <summary>
    /// Response schema names and field descriptions used for OpenAI response formats.
    /// </summary>
    public OpenAiSchemaTemplates Schemas { get; init; } = new();
}

/// <summary>
/// Schema names and field descriptions for OpenAI response format metadata.
/// Keep these aligned with your JSON schema / response_format definitions.
/// </summary>
public sealed record OpenAiSchemaTemplates
{
    // ---------------------------
    // Existing schema names (kept for compatibility)
    // ---------------------------

    public string BehaviourSummaryName { get; init; } = "review_behaviour_summary";
    public string BehaviourSummaryDescription { get; init; } = "Behavior summary for a single file.";

    public string ReviewerQuestionsName { get; init; } = "review_reviewer_questions";
    public string ReviewerQuestionsDescription { get; init; } = "Reviewer questions for a single file.";

    public string ReviewSummaryName { get; init; } = "review_summary_overview";
    public string ReviewSummaryDescription { get; init; } = "Overall summary for a review session.";

    public string ReviewSessionName { get; init; } = "review_session_bundle";
    public string ReviewSessionDescription { get; init; } = "Combined review summary with per-file review details.";

    public string ReviewSessionSummaryDescription { get; init; } =
        "Overall summary for the review session (overall/before/after).";

    public string ReviewSessionFilesDescription { get; init; } =
        "Per-file review outputs keyed by node id and path.";

    public string ReviewSessionFileDescription { get; init; } =
        "Review output for a single file node.";

    public string ReviewSessionFileNodeIdDescription { get; init; } =
        "Node id from the input prompt. Use exactly as provided.";

    public string ReviewSessionFilePathDescription { get; init; } =
        "File path from the input prompt. Use exactly as provided.";

    // ---------------------------
    // New AI-native schemas (recommended)
    // ---------------------------

    /// <summary>
    /// Schema name for node-level AI review output.
    /// This is the primary AI artifact that drives UI.
    /// </summary>
    public string NodeReviewName { get; init; } = "yotei_node_review_v1";

    public string NodeReviewDescription { get; init; } =
        "AI-native review output for a single node. Behaviour-first, evidence-grounded, UI-ready.";

    public string NodeReviewSummaryDescription { get; init; } =
        "One sentence describing the real behavioural change (not 'file modified').";

    public string BehaviourChangesDescription { get; init; } =
        "Concrete behaviour changes derived from diff.";

    public string ExecutionPathsDescription { get; init; } =
        "How/when code runs, with confidence and evidence tokens.";

    public string SideEffectsDescription { get; init; } =
        "External effects introduced/modified (db/network/queue/filesystem/email), with evidence tokens.";

    public string RiskTagsDescription { get; init; } =
        "Risk tags derived from diff: money/auth/data/async/external/perf/none.";

    public string TargetedReviewerQuestionsDescription { get; init; } =
        "Specific, actionable reviewer questions derived from diff and risks.";

    public string TestSuggestionsDescription { get; init; } =
        "Suggested missing tests or assertions that increase confidence.";

    public string UnknownsDescription { get; init; } =
        "Important unknowns not inferable from the diff/context.";

    /// <summary>
    /// Schema name for the execution flow graph powering animations.
    /// </summary>
    public string FlowGraphName { get; init; } = "yotei_flow_graph_v1";

    public string FlowGraphDescription { get; init; } =
        "Execution-impact graph (nodes/edges) for affected routes/flows, including confidence and evidence.";

    public string FlowGraphNodesDescription { get; init; } = "Graph nodes for UI rendering.";
    public string FlowGraphEdgesDescription { get; init; } = "Graph edges for UI rendering.";

    /// <summary>
    /// Schema name for a conversational turn (voice/text) with structured UI updates.
    /// </summary>
    public string ConversationTurnName { get; init; } = "yotei_conversation_turn_v1";

    public string ConversationTurnDescription { get; init; } =
        "Scoped conversational answer plus structured UI updates (checklist/risk/highlights).";

    public string ConversationAnswerDescription { get; init; } =
        "Short, speakable answer suitable for voice playback.";

    public string ConversationEvidenceRefsDescription { get; init; } =
        "Evidence tokens/identifiers from diffs that support the answer.";

    public string SuggestedChecklistItemsDescription { get; init; } =
        "Optional checklist items discovered in conversation.";

    public string HighlightTargetsDescription { get; init; } =
        "What to highlight in UI: diff hunks, files, or flow graph nodes/edges.";

    public string RiskUpdatesDescription { get; init; } =
        "Optional risk tag updates or risk notes based on new reasoning.";

    // ---------------------------
    // Existing field descriptions (kept)
    // ---------------------------

    public string BehaviourChangeDescription { get; init; } = "Concise summary of what behavior changed.";
    public string ScopeDescription { get; init; } = "Scope of impact and confidence.";
    public string ReviewerFocusDescription { get; init; } = "Reviewer focus guidance.";

    public string ReviewerQuestionsDescriptionText { get; init; } = "Targeted reviewer questions.";
    public string ReviewerQuestionItemDescription { get; init; } = "Question text.";

    public string OverallSummaryDescription { get; init; } = "High-level summary of the change.";
    public string BeforeStateDescription { get; init; } = "What the code or behavior was before the change.";
    public string AfterStateDescription { get; init; } = "What the change introduces or does now.";
}
