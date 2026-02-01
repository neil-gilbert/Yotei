namespace Yotei.Api.Features.Flow;

/// <summary>
/// Represents the flow graph for a review session.
/// </summary>
/// <param name="ReviewSessionId">The review session identifier.</param>
/// <param name="CreatedAt">When the graph was generated.</param>
/// <param name="Nodes">The flow nodes in the graph.</param>
/// <param name="Edges">The flow edges connecting nodes.</param>
public record FlowGraphResponse(
    Guid ReviewSessionId,
    DateTimeOffset CreatedAt,
    List<FlowNodeResponse> Nodes,
    List<FlowEdgeResponse> Edges);

/// <summary>
/// Represents a single node in the flow graph.
/// </summary>
/// <param name="Id">The node identifier.</param>
/// <param name="NodeType">The node type (entry, file, side_effect).</param>
/// <param name="Label">The node display label.</param>
/// <param name="Evidence">Evidence strings used to justify the node.</param>
public record FlowNodeResponse(
    Guid Id,
    string NodeType,
    string Label,
    List<string> Evidence);

/// <summary>
/// Represents a directional edge between two flow nodes.
/// </summary>
/// <param name="Id">The edge identifier.</param>
/// <param name="SourceId">The source node identifier.</param>
/// <param name="TargetId">The target node identifier.</param>
/// <param name="Label">The edge label describing the relationship.</param>
public record FlowEdgeResponse(
    Guid Id,
    Guid SourceId,
    Guid TargetId,
    string Label);
