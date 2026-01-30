using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Represents a node in the Domain Knowledge Graph.
/// </summary>
public sealed record DomainNode(
    Guid Id,
    DomainNodeType Type,
    string Title,
    string Description,
    BloomsLevel BloomsLevel,
    double Difficulty,
    double ExamRelevanceWeight,
    IReadOnlyList<string> Tags);

/// <summary>
/// Represents an edge in the Domain Knowledge Graph.
/// </summary>
public sealed record DomainEdge(
    Guid Id,
    Guid FromNodeId,
    Guid ToNodeId,
    DomainEdgeType Type,
    double Strength = 1.0);

/// <summary>
/// Domain Knowledge Graph - represents the structure of the subject domain.
/// This is independent of any learner and contains the "ideal" knowledge structure.
/// </summary>
public sealed class DomainKnowledgeGraph
{
    private readonly Dictionary<Guid, DomainNode> _nodes = new();
    private readonly Dictionary<Guid, DomainEdge> _edges = new();
    private readonly Dictionary<Guid, List<DomainEdge>> _outgoingEdges = new();
    private readonly Dictionary<Guid, List<DomainEdge>> _incomingEdges = new();

    public IReadOnlyDictionary<Guid, DomainNode> Nodes => new ReadOnlyDictionary<Guid, DomainNode>(_nodes);
    public IReadOnlyDictionary<Guid, DomainEdge> Edges => new ReadOnlyDictionary<Guid, DomainEdge>(_edges);

    public DomainKnowledgeGraph() { }

    /// <summary>
    /// Adds a node to the domain graph.
    /// </summary>
    public void AddNode(DomainNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.Id] = node;
        
        if (!_outgoingEdges.ContainsKey(node.Id))
            _outgoingEdges[node.Id] = new List<DomainEdge>();
        if (!_incomingEdges.ContainsKey(node.Id))
            _incomingEdges[node.Id] = new List<DomainEdge>();
    }

    /// <summary>
    /// Adds an edge to the domain graph.
    /// </summary>
    public void AddEdge(DomainEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        
        if (!_nodes.ContainsKey(edge.FromNodeId))
            throw new InvalidOperationException($"Source node {edge.FromNodeId} does not exist.");
        if (!_nodes.ContainsKey(edge.ToNodeId))
            throw new InvalidOperationException($"Target node {edge.ToNodeId} does not exist.");

        _edges[edge.Id] = edge;
        
        _outgoingEdges[edge.FromNodeId].Add(edge);
        _incomingEdges[edge.ToNodeId].Add(edge);
    }

    /// <summary>
    /// Gets a node by ID.
    /// </summary>
    public DomainNode? GetNode(Guid id) => _nodes.GetValueOrDefault(id);

    /// <summary>
    /// Gets all nodes of a specific type.
    /// </summary>
    public IReadOnlyList<DomainNode> GetNodesByType(DomainNodeType type)
        => _nodes.Values.Where(n => n.Type == type).ToList();

    /// <summary>
    /// Gets prerequisites for a node.
    /// </summary>
    public IReadOnlyList<DomainNode> GetPrerequisites(Guid nodeId)
    {
        if (!_outgoingEdges.TryGetValue(nodeId, out var edges))
            return Array.Empty<DomainNode>();

        return edges
            .Where(e => e.Type == DomainEdgeType.PrerequisiteOf)
            .Select(e => GetNode(e.ToNodeId))
            .Where(n => n is not null)
            .Cast<DomainNode>()
            .ToList();
    }

    /// <summary>
    /// Gets nodes that are part of a larger concept.
    /// </summary>
    public IReadOnlyList<DomainNode> GetParts(Guid nodeId)
    {
        if (!_incomingEdges.TryGetValue(nodeId, out var edges))
            return Array.Empty<DomainNode>();

        return edges
            .Where(e => e.Type == DomainEdgeType.PartOf)
            .Select(e => GetNode(e.FromNodeId))
            .Where(n => n is not null)
            .Cast<DomainNode>()
            .ToList();
    }

    /// <summary>
    /// Gets related nodes.
    /// </summary>
    public IReadOnlyList<DomainNode> GetRelatedNodes(Guid nodeId)
    {
        var related = new List<DomainNode>();

        // Check outgoing edges
        if (_outgoingEdges.TryGetValue(nodeId, out var outgoing))
        {
            related.AddRange(outgoing
                .Where(e => e.Type == DomainEdgeType.RelatedTo)
                .Select(e => GetNode(e.ToNodeId))
                .Where(n => n is not null)
                .Cast<DomainNode>());
        }

        // Check incoming edges
        if (_incomingEdges.TryGetValue(nodeId, out var incoming))
        {
            related.AddRange(incoming
                .Where(e => e.Type == DomainEdgeType.RelatedTo)
                .Select(e => GetNode(e.FromNodeId))
                .Where(n => n is not null)
                .Cast<DomainNode>());
        }

        return related.Distinct().ToList();
    }

    /// <summary>
    /// Gets nodes that contrast with the given node.
    /// </summary>
    public IReadOnlyList<DomainNode> GetContrastingNodes(Guid nodeId)
    {
        var contrasting = new List<DomainNode>();

        // Check outgoing edges
        if (_outgoingEdges.TryGetValue(nodeId, out var outgoing))
        {
            contrasting.AddRange(outgoing
                .Where(e => e.Type == DomainEdgeType.ContrastsWith)
                .Select(e => GetNode(e.ToNodeId))
                .Where(n => n is not null)
                .Cast<DomainNode>());
        }

        // Check incoming edges
        if (_incomingEdges.TryGetValue(nodeId, out var incoming))
        {
            contrasting.AddRange(incoming
                .Where(e => e.Type == DomainEdgeType.ContrastsWith)
                .Select(e => GetNode(e.FromNodeId))
                .Where(n => n is not null)
                .Cast<DomainNode>());
        }

        return contrasting.Distinct().ToList();
    }

    /// <summary>
    /// Gets all concepts sorted by exam relevance.
    /// </summary>
    public IReadOnlyList<DomainNode> GetConceptsByExamRelevance()
        => _nodes.Values
            .Where(n => n.Type == DomainNodeType.Concept)
            .OrderByDescending(n => n.ExamRelevanceWeight)
            .ToList();

    /// <summary>
    /// Gets nodes by Bloom's level.
    /// </summary>
    public IReadOnlyList<DomainNode> GetNodesByBloomsLevel(BloomsLevel level)
        => _nodes.Values.Where(n => n.BloomsLevel == level).ToList();
}
