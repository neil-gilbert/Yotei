using System.Text.RegularExpressions;
using Yotei.Api.Features.Flow.Inference;
using Yotei.Api.Models;
using Yotei.Api.Storage;

namespace Yotei.Api.Features.ReviewSessions;

/// <summary>
/// Builds a review-centric tree and summary using language-agnostic heuristics.
/// </summary>
public sealed class ReviewTreeBuilder
{
    private static readonly IReadOnlyDictionary<string, string[]> RiskKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["money"] =
            [
                "billing",
                "payment",
                "invoice",
                "price",
                "charge",
                "stripe",
                "amount",
                "refund",
                "payout",
                "tax",
                "fee",
                "subscription",
                "plan",
                "trial",
                "balance",
                "credit",
                "debit"
            ],
            ["auth"] =
            [
                "auth",
                "token",
                "jwt",
                "oauth",
                "oidc",
                "permission",
                "role",
                "login",
                "logout",
                "session",
                "mfa",
                "sso",
                "apikey",
                "api key",
                "signin",
                "signup"
            ],
            ["data"] =
            [
                "email",
                "phone",
                "ssn",
                "address",
                "dob",
                "user",
                "pii",
                "gdpr",
                "privacy",
                "profile",
                "customer",
                "account",
                "consent",
                "retention",
                "personal"
            ],
            ["async"] =
            [
                "queue",
                "job",
                "worker",
                "retry",
                "cron",
                "schedule",
                "background",
                "async",
                "await",
                "task"
            ],
            ["external"] =
            [
                "http",
                "https",
                "api",
                "client",
                "sdk",
                "webhook",
                "third-party",
                "third party",
                "integration",
                "external",
                "upstream",
                "downstream"
            ]
        };

    private static readonly IReadOnlyDictionary<string, string[]> SideEffectKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["db"] = ["sql", "db", "repository", "migration"],
            ["network"] = ["http", "fetch", "axios", "curl", "client"],
            ["filesystem"] = ["file", "fs", "s3", "write", "upload"],
            ["messaging"] = ["queue", "kafka", "sns", "sqs", "pubsub"],
            ["email"] = ["email", "smtp", "sendgrid", "mail"]
        };

    private static readonly string[] EntryPointKeywords =
    [
        "controller",
        "route",
        "handler",
        "api",
        "endpoint",
        "job",
        "worker",
        "cron",
        "scheduler"
    ];

    private readonly FlowInferenceAdapterRegistry adapterRegistry;

    /// <summary>
    /// Initializes the review tree builder with flow inference adapters.
    /// </summary>
    /// <param name="adapterRegistry">Registry used to resolve flow inference adapters.</param>
    public ReviewTreeBuilder(FlowInferenceAdapterRegistry adapterRegistry)
    {
        this.adapterRegistry = adapterRegistry ?? throw new ArgumentNullException(nameof(adapterRegistry));
    }

    /// <summary>
    /// Builds the review summary and review nodes for a given snapshot.
    /// </summary>
    /// <param name="session">The review session that owns the nodes.</param>
    /// <param name="snapshot">The snapshot to analyze.</param>
    /// <param name="storage">The raw diff storage provider.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A build result containing the summary and nodes.</returns>
    public async Task<ReviewBuildResult> BuildAsync(
        ReviewSession session,
        PullRequestSnapshot snapshot,
        IRawDiffStorage storage,
        CancellationToken cancellationToken)
    {
        var nodes = new List<ReviewNode>();
        var entryPoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var riskEvidence = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var sideEffectEvidence = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var fileRiskEvidence = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        var fileSideEffectEvidence = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
        var repoKey = snapshot.Repository is not null
            ? $"{snapshot.Repository.Owner}/{snapshot.Repository.Name}"
            : null;

        foreach (var change in snapshot.FileChanges)
        {
            var changeType = NormalizeChangeType(change.ChangeType);
            var fileNode = new ReviewNode
            {
                ReviewSession = session,
                NodeType = "file",
                Label = change.Path,
                Path = change.Path,
                ChangeType = changeType,
                Evidence = [$"path:{change.Path}"]
            };

            var diffText = change.RawDiffText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(diffText) && !string.IsNullOrWhiteSpace(change.RawDiffRef))
            {
                diffText = await storage.GetDiffAsync(change.RawDiffRef, cancellationToken) ?? string.Empty;
            }

            var combinedText = $"{change.Path}\n{diffText}";
            var matchedRisks = CollectMatches(combinedText, RiskKeywords);
            var matchedSideEffects = CollectMatches(combinedText, SideEffectKeywords);
            var diffParse = ParseDiff(diffText);
            var riskLineEvidence = CollectLineEvidence(diffParse.Lines, RiskKeywords);
            var sideLineEvidence = CollectLineEvidence(diffParse.Lines, SideEffectKeywords);
            var hunkEvidence = BuildHunkEvidence(diffParse.Hunks);
            var adapter = adapterRegistry.ResolveAdapter(repoKey, change.Path);
            var inference = adapter.Infer(
                new FlowInferenceRequest(change.Path, diffText, adapter.Language),
                cancellationToken);

            foreach (var tag in matchedRisks.Keys)
            {
                fileNode.RiskTags.Add(tag);
            }

            fileNode.RiskTags = fileNode.RiskTags
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
            fileNode.RiskSeverity = DetermineRiskSeverity(fileNode.RiskTags);

            var fileEvidence = new List<string>
            {
                $"path:{change.Path}",
                $"riskSeverity:{fileNode.RiskSeverity}"
            };

            foreach (var tag in matchedRisks.Keys)
            {
                fileEvidence.Add($"risk:{tag}");
            }

            foreach (var tag in matchedSideEffects.Keys)
            {
                fileEvidence.Add($"sideEffect:{tag}");
            }

            fileEvidence.AddRange(BuildKeywordEvidence(matchedRisks, riskLineEvidence));
            fileEvidence.AddRange(BuildKeywordEvidence(matchedSideEffects, sideLineEvidence));
            fileEvidence.AddRange(hunkEvidence);
            ApplyInferenceSignals(
                change.Path,
                inference,
                entryPoints,
                sideEffectEvidence,
                fileSideEffectEvidence,
                fileEvidence);

            fileNode.Evidence = OrderEvidence(fileEvidence);

            if (IsEntryPoint(change.Path, diffText))
            {
                entryPoints.Add(change.Path);
            }

            nodes.Add(fileNode);

            foreach (var (tag, matches) in matchedRisks)
            {
                if (!riskEvidence.TryGetValue(tag, out var evidence))
                {
                    evidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    riskEvidence[tag] = evidence;
                }

                if (!fileRiskEvidence.TryGetValue(change.Path, out var fileEvidenceMap))
                {
                    fileEvidenceMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    fileRiskEvidence[change.Path] = fileEvidenceMap;
                }

                if (!fileEvidenceMap.TryGetValue(tag, out var fileTagEvidence))
                {
                    fileTagEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    fileEvidenceMap[tag] = fileTagEvidence;
                }

                evidence.Add($"path:{change.Path}");
                evidence.Add($"riskSeverity:{fileNode.RiskSeverity}");
                fileTagEvidence.Add($"path:{change.Path}");
                fileTagEvidence.Add($"riskSeverity:{fileNode.RiskSeverity}");

                if (riskLineEvidence.TryGetValue(tag, out var lineEvidence))
                {
                    foreach (var item in lineEvidence)
                    {
                        evidence.Add(item);
                        fileTagEvidence.Add(item);
                    }
                }

                foreach (var match in matches)
                {
                    var hasLineScoped = riskLineEvidence.TryGetValue(tag, out var scopedEvidence) &&
                        scopedEvidence.Any(item =>
                            item.StartsWith($"keyword:{match}@", StringComparison.OrdinalIgnoreCase));
                    if (hasLineScoped)
                    {
                        continue;
                    }

                    var keywordEvidence = $"keyword:{match}";
                    evidence.Add(keywordEvidence);
                    fileTagEvidence.Add(keywordEvidence);
                }
            }

            foreach (var (tag, matches) in matchedSideEffects)
            {
                if (!sideEffectEvidence.TryGetValue(tag, out var evidence))
                {
                    evidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    sideEffectEvidence[tag] = evidence;
                }

                if (!fileSideEffectEvidence.TryGetValue(change.Path, out var fileEvidenceMap))
                {
                    fileEvidenceMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    fileSideEffectEvidence[change.Path] = fileEvidenceMap;
                }

                if (!fileEvidenceMap.TryGetValue(tag, out var fileTagEvidence))
                {
                    fileTagEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    fileEvidenceMap[tag] = fileTagEvidence;
                }

                evidence.Add($"path:{change.Path}");
                fileTagEvidence.Add($"path:{change.Path}");

                if (sideLineEvidence.TryGetValue(tag, out var lineEvidence))
                {
                    foreach (var item in lineEvidence)
                    {
                        evidence.Add(item);
                        fileTagEvidence.Add(item);
                    }
                }

                foreach (var match in matches)
                {
                    var hasLineScoped = sideLineEvidence.TryGetValue(tag, out var scopedEvidence) &&
                        scopedEvidence.Any(item =>
                            item.StartsWith($"keyword:{match}@", StringComparison.OrdinalIgnoreCase));
                    if (hasLineScoped)
                    {
                        continue;
                    }

                    var keywordEvidence = $"keyword:{match}";
                    evidence.Add(keywordEvidence);
                    fileTagEvidence.Add(keywordEvidence);
                }
            }

            foreach (var hunk in diffParse.Hunks)
            {
                var hunkNode = new ReviewNode
                {
                    ReviewSession = session,
                    Parent = fileNode,
                    NodeType = "hunk",
                    Label = hunk.Header,
                    Path = change.Path,
                    ChangeType = changeType,
                    RiskTags = new List<string>(fileNode.RiskTags),
                    RiskSeverity = fileNode.RiskSeverity,
                    Evidence = OrderEvidence(BuildHunkNodeEvidence(change.Path, hunk))
                };

                nodes.Add(hunkNode);
            }
        }

        var summary = new ReviewSummary
        {
            ReviewSession = session,
            ChangedFilesCount = snapshot.FileChanges.Count,
            NewFilesCount = snapshot.FileChanges.Count(change => IsAdded(change.ChangeType)),
            ModifiedFilesCount = snapshot.FileChanges.Count(change => IsModified(change.ChangeType)),
            DeletedFilesCount = snapshot.FileChanges.Count(change => IsDeleted(change.ChangeType)),
            EntryPoints = entryPoints.ToList(),
            SideEffects = sideEffectEvidence.Keys.OrderBy(tag => tag).ToList(),
            RiskTags = riskEvidence.Keys.OrderBy(tag => tag).ToList(),
            TopPaths = snapshot.FileChanges
                .OrderByDescending(change => change.AddedLines + change.DeletedLines)
                .Take(5)
                .Select(change => change.Path)
                .ToList()
        };

        var overviewGroup = new ReviewNode
        {
            ReviewSession = session,
            NodeType = "group",
            Label = "Overview",
            ChangeType = "modified"
        };

        nodes.Add(overviewGroup);

        var summaryNode = new ReviewNode
        {
            ReviewSession = session,
            Parent = overviewGroup,
            NodeType = "summary",
            Label = $"{summary.ChangedFilesCount} files changed ({summary.NewFilesCount} new, {summary.ModifiedFilesCount} modified, {summary.DeletedFilesCount} deleted)",
            ChangeType = "modified",
            Evidence = summary.TopPaths.Select(path => $"path:{path}").ToList()
        };

        nodes.Add(summaryNode);

        foreach (var entryPoint in entryPoints.OrderBy(item => item))
        {
            var entryNode = new ReviewNode
            {
                ReviewSession = session,
                Parent = overviewGroup,
                NodeType = "entry_point",
                Label = entryPoint,
                ChangeType = "modified",
                Evidence = [$"path:{entryPoint}"]
            };

            nodes.Add(entryNode);
        }

        var fileNodes = nodes.Where(node => node.NodeType == "file").ToList();
        foreach (var fileNode in fileNodes)
        {
            var path = fileNode.Path ?? fileNode.Label;
            var fileRiskTags = fileNode.RiskTags.ToList();
            var fileRiskEvidenceMap = fileRiskEvidence.TryGetValue(path, out var riskMap)
                ? riskMap
                : new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var fileSideEvidenceMap = fileSideEffectEvidence.TryGetValue(path, out var sideMap)
                ? sideMap
                : new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var riskTag in fileRiskTags.OrderBy(tag => tag))
            {
                var evidence = fileRiskEvidenceMap.TryGetValue(riskTag, out var items)
                    ? items
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $"path:{path}" };

                var riskNode = new ReviewNode
                {
                    ReviewSession = session,
                    Parent = fileNode,
                    NodeType = "risk",
                    Label = riskTag,
                    ChangeType = fileNode.ChangeType,
                    RiskTags = [riskTag],
                    RiskSeverity = fileNode.RiskSeverity,
                    Evidence = OrderEvidence(evidence),
                    Path = path
                };

                nodes.Add(riskNode);
            }

            foreach (var (effect, evidence) in fileSideEvidenceMap.OrderBy(pair => pair.Key))
            {
                var effectNode = new ReviewNode
                {
                    ReviewSession = session,
                    Parent = fileNode,
                    NodeType = "side_effect",
                    Label = effect,
                    ChangeType = fileNode.ChangeType,
                    RiskSeverity = fileNode.RiskSeverity,
                    Evidence = OrderEvidence(evidence),
                    Path = path
                };

                nodes.Add(effectNode);
            }

            var checklistNode = new ReviewNode
            {
                ReviewSession = session,
                Parent = fileNode,
                NodeType = "checklist",
                Label = "Review checklist",
                ChangeType = fileNode.ChangeType,
                RiskSeverity = fileNode.RiskSeverity,
                Evidence = OrderEvidence(fileRiskTags.Select(tag => $"risk:{tag}").ToList()),
                Path = path
            };

            nodes.Add(checklistNode);
        }

        return new ReviewBuildResult(summary, nodes);
    }

    /// <summary>
    /// Normalizes change types into the review model vocabulary.
    /// </summary>
    private static string NormalizeChangeType(string changeType)
    {
        if (IsAdded(changeType))
        {
            return "new";
        }

        if (IsDeleted(changeType))
        {
            return "deleted";
        }

        return "modified";
    }

    /// <summary>
    /// Determines whether a change type represents an addition.
    /// </summary>
    private static bool IsAdded(string changeType)
    {
        return string.Equals(changeType, "added", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(changeType, "new", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a change type represents a deletion.
    /// </summary>
    private static bool IsDeleted(string changeType)
    {
        return string.Equals(changeType, "deleted", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(changeType, "removed", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a change type represents a modification.
    /// </summary>
    private static bool IsModified(string changeType)
    {
        return string.Equals(changeType, "modified", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Collects keyword matches for the provided category map.
    /// </summary>
    private static Dictionary<string, List<string>> CollectMatches(string text, IReadOnlyDictionary<string, string[]> keywords)
    {
        var matches = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tag, words) in keywords)
        {
            var found = words
                .Where(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (found.Count > 0)
            {
                matches[tag] = found;
            }
        }

        return matches;
    }

    /// <summary>
    /// Extracts entry-point signals from file paths or diff content.
    /// </summary>
    private static bool IsEntryPoint(string path, string diff)
    {
        foreach (var keyword in EntryPointKeywords)
        {
            if (path.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                diff.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines the risk severity based on tag combinations.
    /// </summary>
    private static string DetermineRiskSeverity(IReadOnlyCollection<string> riskTags)
    {
        if (riskTags.Count == 0)
        {
            return "low";
        }

        var normalized = riskTags
            .Select(tag => tag.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalized switch
        {
            var set when set.Contains("money") &&
                           (set.Contains("auth") || set.Contains("data") || set.Contains("external")) => "high",
            var set when set.Contains("auth") && set.Contains("external") => "high",
            var set when set.Contains("data") && set.Contains("external") => "high",
            var set when set.Count >= 3 => "high",
            var set when set.Count == 2 => "medium",
            _ => "low"
        };
    }

    /// <summary>
    /// Parses diff content into line and hunk metadata for evidence capture.
    /// </summary>
    private static DiffParseResult ParseDiff(string diff)
    {
        var lines = new List<DiffLineInfo>();
        var hunks = new List<DiffHunk>();

        if (string.IsNullOrWhiteSpace(diff))
        {
            return new DiffParseResult(lines, hunks);
        }

        var oldLine = (int?)null;
        var newLine = (int?)null;
        string? currentHunk = null;

        foreach (var rawLine in diff.Replace("\r", string.Empty).Split('\n'))
        {
            if (rawLine.StartsWith("@@"))
            {
                currentHunk = rawLine.Trim();
                if (TryParseHunkHeader(currentHunk, out var oldStart, out var newStart, out var newCount))
                {
                    oldLine = oldStart;
                    newLine = newStart;
                    hunks.Add(new DiffHunk(currentHunk, newStart, newCount));
                }
                else
                {
                    oldLine = null;
                    newLine = null;
                    hunks.Add(new DiffHunk(currentHunk, null, null));
                }

                continue;
            }

            if (currentHunk is null)
            {
                continue;
            }

            if (rawLine.StartsWith("+++ ", StringComparison.OrdinalIgnoreCase) ||
                rawLine.StartsWith("--- ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rawLine.StartsWith("+", StringComparison.Ordinal))
            {
                var lineRef = newLine is null ? null : $"+{newLine}";
                lines.Add(new DiffLineInfo(rawLine[1..], lineRef, currentHunk));
                if (newLine is not null)
                {
                    newLine++;
                }

                continue;
            }

            if (rawLine.StartsWith("-", StringComparison.Ordinal))
            {
                var lineRef = oldLine is null ? null : $"-{oldLine}";
                lines.Add(new DiffLineInfo(rawLine[1..], lineRef, currentHunk));
                if (oldLine is not null)
                {
                    oldLine++;
                }

                continue;
            }

            var contextLine = rawLine.StartsWith(" ", StringComparison.Ordinal) ? rawLine[1..] : rawLine;
            lines.Add(new DiffLineInfo(contextLine, null, currentHunk));
            if (oldLine is not null)
            {
                oldLine++;
            }

            if (newLine is not null)
            {
                newLine++;
            }
        }

        return new DiffParseResult(lines, hunks);
    }

    /// <summary>
    /// Attempts to parse a unified diff hunk header into line ranges.
    /// </summary>
    private static bool TryParseHunkHeader(string header, out int oldStart, out int newStart, out int newCount)
    {
        oldStart = 0;
        newStart = 0;
        newCount = 0;

        var match = Regex.Match(header, @"@@ -(?<oldStart>\d+)(,(?<oldCount>\d+))? \+(?<newStart>\d+)(,(?<newCount>\d+))? @@");
        if (!match.Success)
        {
            return false;
        }

        oldStart = int.Parse(match.Groups["oldStart"].Value);
        newStart = int.Parse(match.Groups["newStart"].Value);
        newCount = match.Groups["newCount"].Success
            ? int.Parse(match.Groups["newCount"].Value)
            : 1;

        return true;
    }

    /// <summary>
    /// Builds hunk evidence strings from parsed hunk metadata.
    /// </summary>
    private static List<string> BuildHunkEvidence(IReadOnlyList<DiffHunk> hunks)
    {
        var evidence = new List<string>();
        foreach (var hunk in hunks)
        {
            evidence.Add($"hunk:{hunk.Header}");

            if (hunk.NewStart is not null && hunk.NewCount is not null)
            {
                var start = hunk.NewStart.Value;
                var end = start + Math.Max(hunk.NewCount.Value - 1, 0);
                evidence.Add($"hunk:+{start}..+{end}");
            }
        }

        return evidence;
    }

    /// <summary>
    /// Builds the evidence list for a hunk node.
    /// </summary>
    private static List<string> BuildHunkNodeEvidence(string path, DiffHunk hunk)
    {
        var evidence = new List<string>
        {
            $"path:{path}",
            $"hunk:{hunk.Header}"
        };

        if (hunk.NewStart is not null && hunk.NewCount is not null)
        {
            var start = hunk.NewStart.Value;
            var end = start + Math.Max(hunk.NewCount.Value - 1, 0);
            evidence.Add($"hunk:+{start}..+{end}");
        }

        return evidence;
    }

    /// <summary>
    /// Collects keyword evidence with line and hunk context.
    /// </summary>
    private static Dictionary<string, HashSet<string>> CollectLineEvidence(
        IReadOnlyList<DiffLineInfo> lines,
        IReadOnlyDictionary<string, string[]> keywords)
    {
        var evidence = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            foreach (var (tag, words) in keywords)
            {
                foreach (var word in words)
                {
                    if (!line.Text.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!evidence.TryGetValue(tag, out var items))
                    {
                        items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        evidence[tag] = items;
                    }

                    var keywordEvidence = line.LineReference is null
                        ? $"keyword:{word}"
                        : $"keyword:{word}@{line.LineReference}";
                    items.Add(keywordEvidence);

                    if (!string.IsNullOrWhiteSpace(line.HunkHeader))
                    {
                        items.Add($"hunk:{line.HunkHeader}");
                    }
                }
            }
        }

        return evidence;
    }

    /// <summary>
    /// Builds keyword evidence from matched tags and line evidence.
    /// </summary>
    private static List<string> BuildKeywordEvidence(
        IReadOnlyDictionary<string, List<string>> matches,
        IReadOnlyDictionary<string, HashSet<string>> lineEvidence)
    {
        var evidence = new List<string>();

        foreach (var (tag, words) in matches)
        {
            if (lineEvidence.TryGetValue(tag, out var lineItems))
            {
                evidence.AddRange(lineItems);
            }

            foreach (var word in words)
            {
                var lineScoped = lineItems?.Any(item =>
                        item.StartsWith($"keyword:{word}@", StringComparison.OrdinalIgnoreCase)) ??
                    false;

                if (!lineScoped)
                {
                    evidence.Add($"keyword:{word}");
                }
            }
        }

        return evidence;
    }

    /// <summary>
    /// Orders and deduplicates evidence tags for consistent output.
    /// </summary>
    private static List<string> OrderEvidence(IEnumerable<string> evidence)
    {
        return evidence
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => GetEvidenceRank(item))
            .ThenBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Assigns ordering ranks for evidence tags.
    /// </summary>
    private static int GetEvidenceRank(string evidence)
    {
        return evidence switch
        {
            var item when item.StartsWith("path:", StringComparison.OrdinalIgnoreCase) => 0,
            var item when item.StartsWith("riskSeverity:", StringComparison.OrdinalIgnoreCase) => 1,
            var item when item.StartsWith("risk:", StringComparison.OrdinalIgnoreCase) => 2,
            var item when item.StartsWith("sideEffect:", StringComparison.OrdinalIgnoreCase) => 3,
            var item when item.StartsWith("entryPoint:", StringComparison.OrdinalIgnoreCase) => 4,
            var item when item.StartsWith("keyword:", StringComparison.OrdinalIgnoreCase) => 5,
            var item when item.StartsWith("hunk:", StringComparison.OrdinalIgnoreCase) => 6,
            _ => 7
        };
    }

    /// <summary>
    /// Applies flow inference signals into the review tree evidence maps.
    /// </summary>
    /// <param name="path">The file path for the change.</param>
    /// <param name="inference">The flow inference result.</param>
    /// <param name="entryPoints">The entry point set to update.</param>
    /// <param name="sideEffectEvidence">The global side-effect evidence map.</param>
    /// <param name="fileSideEffectEvidence">The per-file side-effect evidence map.</param>
    /// <param name="fileEvidence">The file-level evidence list to update.</param>
    private static void ApplyInferenceSignals(
        string path,
        FlowInferenceResult inference,
        HashSet<string> entryPoints,
        IDictionary<string, HashSet<string>> sideEffectEvidence,
        IDictionary<string, Dictionary<string, HashSet<string>>> fileSideEffectEvidence,
        List<string> fileEvidence)
    {
        foreach (var entry in inference.EntryPoints)
        {
            if (string.IsNullOrWhiteSpace(entry.Label))
            {
                continue;
            }

            entryPoints.Add(entry.Label);
            fileEvidence.Add($"entryPoint:{entry.Label}");
            AppendEvidence(fileEvidence, entry.Evidence);
        }

        foreach (var sideEffect in inference.SideEffects)
        {
            if (string.IsNullOrWhiteSpace(sideEffect.Label))
            {
                continue;
            }

            if (!sideEffectEvidence.TryGetValue(sideEffect.Label, out var evidence))
            {
                evidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                sideEffectEvidence[sideEffect.Label] = evidence;
            }

            if (!fileSideEffectEvidence.TryGetValue(path, out var fileEvidenceMap))
            {
                fileEvidenceMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                fileSideEffectEvidence[path] = fileEvidenceMap;
            }

            if (!fileEvidenceMap.TryGetValue(sideEffect.Label, out var fileTagEvidence))
            {
                fileTagEvidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                fileEvidenceMap[sideEffect.Label] = fileTagEvidence;
            }

            evidence.Add($"path:{path}");
            fileTagEvidence.Add($"path:{path}");
            AddEvidenceEntries(evidence, sideEffect.Evidence);
            AddEvidenceEntries(fileTagEvidence, sideEffect.Evidence);
            fileEvidence.Add($"sideEffect:{sideEffect.Label}");
            AppendEvidence(fileEvidence, sideEffect.Evidence);
        }
    }

    /// <summary>
    /// Adds evidence entries into the provided set.
    /// </summary>
    /// <param name="target">The set to update.</param>
    /// <param name="entries">The evidence entries to add.</param>
    private static void AddEvidenceEntries(HashSet<string> target, IEnumerable<string> entries)
    {
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry))
            {
                target.Add(entry.Trim());
            }
        }
    }

    /// <summary>
    /// Adds evidence entries into the provided list.
    /// </summary>
    /// <param name="target">The list to update.</param>
    /// <param name="entries">The evidence entries to add.</param>
    private static void AppendEvidence(List<string> target, IEnumerable<string> entries)
    {
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry))
            {
                target.Add(entry.Trim());
            }
        }
    }

    /// <summary>
    /// Represents parsed diff metadata for evidence extraction.
    /// </summary>
    private sealed record DiffParseResult(
        IReadOnlyList<DiffLineInfo> Lines,
        IReadOnlyList<DiffHunk> Hunks);

    /// <summary>
    /// Represents a diff line with optional line and hunk context.
    /// </summary>
    private sealed record DiffLineInfo(
        string Text,
        string? LineReference,
        string? HunkHeader);

    /// <summary>
    /// Represents a diff hunk with parsed line range metadata.
    /// </summary>
    private sealed record DiffHunk(
        string Header,
        int? NewStart,
        int? NewCount);
}

/// <summary>
/// Contains the output of a review tree build operation.
/// </summary>
/// <param name="Summary">The generated summary for the review session.</param>
/// <param name="Nodes">The review nodes that make up the comprehension tree.</param>
public sealed record ReviewBuildResult(ReviewSummary Summary, List<ReviewNode> Nodes);
