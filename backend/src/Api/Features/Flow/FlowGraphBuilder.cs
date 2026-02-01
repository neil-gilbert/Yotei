using System.Security.Cryptography;
using System.Text;
using Yotei.Api.Models;

namespace Yotei.Api.Features.Flow;

/// <summary>
/// Builds a static execution flow graph from review nodes using heuristics.
/// </summary>
public sealed class FlowGraphBuilder
{
    private static readonly StringComparer NodeComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly string[] SideEffectPrefixes = ["sideEffect:"];

    /// <summary>
    /// Builds flow graph nodes and edges for the provided review nodes.
    /// </summary>
    /// <param name="reviewNodes">The review nodes available for the session.</param>
    /// <returns>A result containing flow nodes and edges.</returns>
    public FlowGraphResult Build(IReadOnlyList<ReviewNode> reviewNodes)
    {
        if (reviewNodes is null)
        {
            throw new ArgumentNullException(nameof(reviewNodes));
        }

        var fileNodes = reviewNodes
            .Where(node => string.Equals(node.NodeType, "file", StringComparison.OrdinalIgnoreCase))
            .OrderBy(node => node.Label, NodeComparer)
            .ToList();

        var entryNodes = reviewNodes
            .Where(node => string.Equals(node.NodeType, "entry_point", StringComparison.OrdinalIgnoreCase))
            .OrderBy(node => node.Label, NodeComparer)
            .ToList();

        var sideEffectNodes = reviewNodes
            .Where(node => string.Equals(node.NodeType, "side_effect", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var flowNodes = new List<FlowNodeResponse>();
        flowNodes.AddRange(entryNodes.Select(node =>
            new FlowNodeResponse(node.Id, "entry", node.Label, BuildEvidence(node))));
        flowNodes.AddRange(fileNodes.Select(node =>
            new FlowNodeResponse(node.Id, "file", node.Label, BuildEvidence(node))));

        var sideEffectLookup = BuildSideEffectNodes(sideEffectNodes, fileNodes);
        flowNodes.AddRange(sideEffectLookup.Values.OrderBy(node => node.Label, NodeComparer));

        var edges = new List<FlowEdgeResponse>();
        edges.AddRange(BuildEntryEdges(entryNodes, fileNodes));
        edges.AddRange(BuildSideEffectEdges(fileNodes, sideEffectLookup));

        return new FlowGraphResult(flowNodes, edges);
    }

    // Builds evidence list for a flow node, de-duplicated for readability.
    private static List<string> BuildEvidence(ReviewNode node)
    {
        return node.Evidence
            .Distinct(NodeComparer)
            .Take(6)
            .ToList();
    }

    // Builds side-effect flow nodes from review side-effect nodes and file evidence tags.
    private static Dictionary<string, FlowNodeResponse> BuildSideEffectNodes(
        IEnumerable<ReviewNode> sideEffectNodes,
        IEnumerable<ReviewNode> fileNodes)
    {
        var evidenceLookup = new Dictionary<string, HashSet<string>>(NodeComparer);

        foreach (var node in sideEffectNodes)
        {
            if (!evidenceLookup.TryGetValue(node.Label, out var evidence))
            {
                evidence = new HashSet<string>(NodeComparer);
                evidenceLookup[node.Label] = evidence;
            }

            foreach (var item in node.Evidence)
            {
                evidence.Add(item);
            }
        }

        foreach (var fileNode in fileNodes)
        {
            foreach (var sideEffect in ExtractEvidence(fileNode, SideEffectPrefixes))
            {
                if (!evidenceLookup.TryGetValue(sideEffect, out var evidence))
                {
                    evidence = new HashSet<string>(NodeComparer);
                    evidenceLookup[sideEffect] = evidence;
                }

                evidence.Add($"sideEffect:{sideEffect}");
                evidence.Add($"path:{fileNode.Path ?? fileNode.Label}");
            }
        }

        var nodes = new Dictionary<string, FlowNodeResponse>(NodeComparer);
        foreach (var (label, evidence) in evidenceLookup)
        {
            var nodeId = CreateDeterministicId($"side-effect:{label}");
            nodes[label] = new FlowNodeResponse(nodeId, "side_effect", label, evidence.OrderBy(item => item, NodeComparer).ToList());
        }

        return nodes;
    }

    // Builds edges from entry nodes to matching file nodes.
    private static IEnumerable<FlowEdgeResponse> BuildEntryEdges(
        IEnumerable<ReviewNode> entryNodes,
        IEnumerable<ReviewNode> fileNodes)
    {
        var fileLookup = fileNodes
            .Select(node => new { Key = node.Path ?? node.Label, Node = node })
            .GroupBy(item => item.Key, NodeComparer)
            .ToDictionary(group => group.Key, group => group.First().Node, NodeComparer);

        foreach (var entryNode in entryNodes)
        {
            if (fileLookup.TryGetValue(entryNode.Label, out var fileNode))
            {
                var edgeId = CreateDeterministicId($"edge:entry:{entryNode.Id}:{fileNode.Id}");
                yield return new FlowEdgeResponse(edgeId, entryNode.Id, fileNode.Id, "entry");
            }
        }
    }

    // Builds edges from file nodes to side-effect nodes based on evidence tags.
    private static IEnumerable<FlowEdgeResponse> BuildSideEffectEdges(
        IEnumerable<ReviewNode> fileNodes,
        IReadOnlyDictionary<string, FlowNodeResponse> sideEffectNodes)
    {
        var emitted = new HashSet<string>(NodeComparer);

        foreach (var fileNode in fileNodes)
        {
            var sideEffects = ExtractEvidence(fileNode, SideEffectPrefixes)
                .OrderBy(effect => effect, NodeComparer)
                .ToList();

            foreach (var sideEffect in sideEffects)
            {
                if (!sideEffectNodes.TryGetValue(sideEffect, out var sideNode))
                {
                    continue;
                }

                var key = $"{fileNode.Id}:{sideNode.Id}";
                if (!emitted.Add(key))
                {
                    continue;
                }

                var edgeId = CreateDeterministicId($"edge:side-effect:{fileNode.Id}:{sideNode.Id}");
                yield return new FlowEdgeResponse(edgeId, fileNode.Id, sideNode.Id, "side_effect");
            }
        }
    }

    // Extracts evidence values that match any of the provided prefixes.
    private static List<string> ExtractEvidence(ReviewNode node, IEnumerable<string> prefixes)
    {
        var matches = new List<string>();

        foreach (var evidence in node.Evidence)
        {
            foreach (var prefix in prefixes)
            {
                if (evidence.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(evidence[prefix.Length..]);
                }
            }
        }

        return matches
            .Distinct(NodeComparer)
            .ToList();
    }

    // Creates a deterministic GUID from a stable string seed.
    private static Guid CreateDeterministicId(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

/// <summary>
/// Represents the computed flow graph data used for API responses.
/// </summary>
/// <param name="Nodes">The flow nodes for the response.</param>
/// <param name="Edges">The flow edges for the response.</param>
public sealed record FlowGraphResult(List<FlowNodeResponse> Nodes, List<FlowEdgeResponse> Edges);
