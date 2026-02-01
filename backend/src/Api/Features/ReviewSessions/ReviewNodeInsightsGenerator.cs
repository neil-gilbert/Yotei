using Yotei.Api.Models;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Generates behavior summaries and review checklists for file nodes.
/// </summary>
public sealed class ReviewNodeInsightsGenerator
{
    private static readonly Dictionary<string, string> RiskPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["money"] = "payment or billing logic",
        ["auth"] = "authentication or authorization checks",
        ["data"] = "customer data handling",
        ["async"] = "background or async processing",
        ["external"] = "outbound API calls"
    };

    private static readonly Dictionary<string, string[]> RiskChecklist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["money"] =
        [
            "Are amounts, fees, and taxes computed correctly for edge cases?",
            "Are duplicate charges prevented (idempotency, retries, unique keys)?"
        ],
        ["auth"] =
        [
            "Is authorization enforced on every path and failure handled safely?",
            "Are tokens validated and expired sessions rejected?"
        ],
        ["data"] =
        [
            "Is PII minimized, masked in logs, and stored securely?",
            "Are new fields validated and sanitized before persistence?"
        ],
        ["async"] =
        [
            "Are retries safe and idempotent for background work?",
            "Are failures surfaced (dead-letter, alerts, or retries)?"
        ],
        ["external"] =
        [
            "Are timeouts/retries set for external calls and errors handled?",
            "Is partial failure handled without corrupting state?"
        ]
    };

    private static readonly Dictionary<string, string[]> SeverityChecklist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["high"] =
        [
            "Is there a rollback or feature-flag strategy for this change?",
            "Are monitoring and alerts in place for high-impact failures?"
        ],
        ["medium"] =
        [
            "Are logs and metrics sufficient to diagnose failures quickly?",
            "Are failure modes and retries documented and tested?"
        ]
    };

    private static readonly Dictionary<string, string[]> SideEffectChecklist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["db"] =
        [
            "Are migrations backwards compatible and indexed appropriately?",
            "Are writes wrapped in a transaction where needed?"
        ],
        ["network"] =
        [
            "Is the request retried safely and rate limits handled?",
            "Are responses validated before use?"
        ],
        ["filesystem"] =
        [
            "Are writes atomic and cleanup handled on failure?",
            "Are paths validated to avoid traversal or overwrite?"
        ],
        ["messaging"] =
        [
            "Are messages idempotent and deduplicated?",
            "Is the queue/topic configured for retries and DLQ?"
        ],
        ["email"] =
        [
            "Are recipients verified and email content sanitized?",
            "Are delivery failures handled or retried?"
        ]
    };

    private static readonly string[] ApiKeywords = ["controller", "handler", "route", "api", "endpoint"];
    private static readonly string[] BackgroundKeywords = ["job", "worker", "cron", "scheduler"];

    /// <summary>
    /// Builds a behavior summary for a file node.
    /// </summary>
    /// <param name="fileNode">The file node to summarize.</param>
    /// <param name="change">The file change metadata.</param>
    /// <returns>A populated behavior summary entity.</returns>
    public ReviewNodeBehaviourSummary BuildBehaviourSummary(ReviewNode fileNode, FileChange change)
    {
        var phrases = fileNode.RiskTags
            .Where(tag => RiskPhrases.ContainsKey(tag))
            .Select(tag => RiskPhrases[tag])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var behaviour = change.ChangeType switch
        {
            var type when string.Equals(type, "added", StringComparison.OrdinalIgnoreCase) => "Introduces new behavior",
            var type when string.Equals(type, "deleted", StringComparison.OrdinalIgnoreCase) => "Removes behavior",
            _ => "Changes existing behavior"
        };

        var descriptor = phrases.Count > 0
            ? $"{behaviour} in {string.Join(", ", phrases)}."
            : $"{behaviour} in {fileNode.Label}.";

        var scope = BuildScope(fileNode.Path ?? fileNode.Label);
        var focusItems = BuildReviewerFocus(fileNode);

        return new ReviewNodeBehaviourSummary
        {
            ReviewNodeId = fileNode.Id,
            BehaviourChange = descriptor,
            Scope = scope,
            ReviewerFocus = focusItems
        };
    }

    /// <summary>
    /// Builds a review checklist for a file node.
    /// </summary>
    /// <param name="fileNode">The file node to create a checklist for.</param>
    /// <returns>A populated checklist entity.</returns>
    public ReviewNodeChecklist BuildChecklist(ReviewNode fileNode)
    {
        var items = new List<string>();
        var sideEffects = ExtractEvidence(fileNode, "sideEffect:");

        if (SeverityChecklist.TryGetValue(fileNode.RiskSeverity, out var severityQuestions))
        {
            items.AddRange(severityQuestions);
        }

        foreach (var tag in fileNode.RiskTags)
        {
            if (RiskChecklist.TryGetValue(tag, out var questions))
            {
                items.AddRange(questions);
            }
        }

        foreach (var sideEffect in sideEffects)
        {
            if (SideEffectChecklist.TryGetValue(sideEffect, out var questions))
            {
                items.AddRange(questions);
            }
        }

        var distinct = items
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (distinct.Count == 0)
        {
            distinct.Add("Is the expected behavior covered by tests?");
            distinct.Add("Are edge cases and error handling addressed?");
        }

        var createdAt = DateTimeOffset.UtcNow;
        var detailedItems = distinct
            .Select(item => new ReviewChecklistItem
            {
                Text = item,
                Source = "heuristic",
                CreatedAt = createdAt
            })
            .ToList();

        return new ReviewNodeChecklist
        {
            ReviewNodeId = fileNode.Id,
            Items = distinct,
            ItemsDetailed = detailedItems
        };
    }

    // Infers scope and confidence from path heuristics.
    private static string BuildScope(string path)
    {
        var lower = path.ToLowerInvariant();
        if (ApiKeywords.Any(keyword => lower.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Scope: API entry point (heuristic, high confidence) via {path}.";
        }

        if (BackgroundKeywords.Any(keyword => lower.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Scope: background job/worker (heuristic, high confidence) via {path}.";
        }

        if (lower.Contains("config", StringComparison.OrdinalIgnoreCase) ||
            lower.Contains(".yaml", StringComparison.OrdinalIgnoreCase) ||
            lower.Contains(".json", StringComparison.OrdinalIgnoreCase))
        {
            return $"Scope: configuration or routing change (heuristic, medium confidence) in {path}.";
        }

        return $"Scope: internal module (heuristic, low confidence) in {path}.";
    }

    // Builds the reviewer focus string from risk and side-effect signals.
    private static string BuildReviewerFocus(ReviewNode fileNode)
    {
        var focus = new List<string>();
        var sideEffects = ExtractEvidence(fileNode, "sideEffect:");

        var severityFocus = fileNode.RiskSeverity switch
        {
            "high" => "High severity: validate rollback, monitoring, and access controls.",
            "medium" => "Medium severity: confirm observability and safe failure paths.",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(severityFocus))
        {
            focus.Add(severityFocus);
        }

        foreach (var tag in fileNode.RiskTags)
        {
            if (RiskChecklist.TryGetValue(tag, out var questions))
            {
                focus.AddRange(questions);
            }
        }

        foreach (var sideEffect in sideEffects)
        {
            if (SideEffectChecklist.TryGetValue(sideEffect, out var questions))
            {
                focus.AddRange(questions);
            }
        }

        var unique = focus
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        if (unique.Count == 0)
        {
            unique.Add("Confirm expected behavior with realistic inputs.");
            unique.Add("Verify error handling paths.");
        }

        return $"Reviewer focus: {string.Join(" ", unique)}";
    }

    // Extracts evidence tags with a specific prefix.
    private static List<string> ExtractEvidence(ReviewNode fileNode, string prefix)
    {
        return fileNode.Evidence
            .Where(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(item => item[prefix.Length..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
