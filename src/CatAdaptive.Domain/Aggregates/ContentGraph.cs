using System.Collections.ObjectModel;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Domain.Aggregates;

/// <summary>
/// The Content Graph (Supply) - represents all available instructional content
/// and how it connects. This is the "what we can teach" graph.
/// </summary>
public sealed class ContentGraph
{
    private readonly Dictionary<Guid, ContentNode> _nodes = new();
    private readonly Dictionary<Guid, ContentEdge> _edges = new();
    private readonly Dictionary<Guid, List<ContentEdge>> _outgoingEdges = new();
    private readonly Dictionary<Guid, List<ContentEdge>> _incomingEdges = new();

    public IReadOnlyDictionary<Guid, ContentNode> Nodes => new ReadOnlyDictionary<Guid, ContentNode>(_nodes);
    public IReadOnlyDictionary<Guid, ContentEdge> Edges => new ReadOnlyDictionary<Guid, ContentEdge>(_edges);

    /// <summary>
    /// Gets a node by ID.
    /// </summary>
    public ContentNode? GetNode(Guid id) => _nodes.GetValueOrDefault(id);

    /// <summary>
    /// Gets all nodes of a specific type.
    /// </summary>
    public IReadOnlyList<ContentNode> GetNodesByType(ContentNodeType type)
        => _nodes.Values.Where(n => n.Type == type).ToList();

    /// <summary>
    /// Gets all concept nodes (the primary teachable units).
    /// </summary>
    public IReadOnlyList<ContentNode> GetConcepts()
        => GetNodesByType(ContentNodeType.Concept);

    /// <summary>
    /// Gets all nodes from external sources.
    /// </summary>
    public IReadOnlyList<ContentNode> GetExternalNodes()
        => _nodes.Values.Where(n => n.SourceOrigin == ContentOrigin.External).ToList();

    /// <summary>
    /// Gets prerequisite concepts for a given concept (via DependsOn edges).
    /// </summary>
    public IReadOnlyList<ContentNode> GetPrerequisites(Guid conceptId)
    {
        if (!_outgoingEdges.TryGetValue(conceptId, out var edges))
            return Array.Empty<ContentNode>();

        return edges
            .Where(e => e.Type == ContentEdgeType.DependsOn)
            .Select(e => GetNode(e.ToNodeId))
            .Where(n => n is not null)
            .Cast<ContentNode>()
            .ToList();
    }

    /// <summary>
    /// Gets explanation nodes for a concept (via Explains edges incoming to concept).
    /// </summary>
    public IReadOnlyList<ContentNode> GetExplanations(Guid conceptId)
    {
        if (!_incomingEdges.TryGetValue(conceptId, out var edges))
            return Array.Empty<ContentNode>();

        return edges
            .Where(e => e.Type == ContentEdgeType.Explains)
            .Select(e => GetNode(e.FromNodeId))
            .Where(n => n is not null && n.Type == ContentNodeType.Explanation)
            .Cast<ContentNode>()
            .ToList();
    }

    /// <summary>
    /// Gets example nodes for a concept (via ExampleOf edges incoming to concept).
    /// </summary>
    public IReadOnlyList<ContentNode> GetExamples(Guid conceptId)
    {
        if (!_incomingEdges.TryGetValue(conceptId, out var edges))
            return Array.Empty<ContentNode>();

        return edges
            .Where(e => e.Type == ContentEdgeType.ExampleOf)
            .Select(e => GetNode(e.FromNodeId))
            .Where(n => n is not null && (n.Type == ContentNodeType.Example || n.Type == ContentNodeType.WorkedProblem))
            .Cast<ContentNode>()
            .ToList();
    }

    /// <summary>
    /// Gets concepts that depend on the given concept (reverse prerequisite lookup).
    /// </summary>
    public IReadOnlyList<ContentNode> GetDependents(Guid conceptId)
    {
        if (!_incomingEdges.TryGetValue(conceptId, out var edges))
            return Array.Empty<ContentNode>();

        return edges
            .Where(e => e.Type == ContentEdgeType.DependsOn)
            .Select(e => GetNode(e.FromNodeId))
            .Where(n => n is not null)
            .Cast<ContentNode>()
            .ToList();
    }

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    public void AddNode(ContentNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.Id] = node;
        
        if (!_outgoingEdges.ContainsKey(node.Id))
            _outgoingEdges[node.Id] = new List<ContentEdge>();
        if (!_incomingEdges.ContainsKey(node.Id))
            _incomingEdges[node.Id] = new List<ContentEdge>();
    }

    /// <summary>
    /// Adds an edge to the graph. Both nodes must already exist.
    /// </summary>
    public void AddEdge(ContentEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        
        if (!_nodes.ContainsKey(edge.FromNodeId))
            throw new InvalidOperationException($"Source node {edge.FromNodeId} does not exist.");
        if (!_nodes.ContainsKey(edge.ToNodeId))
            throw new InvalidOperationException($"Target node {edge.ToNodeId} does not exist.");

        _edges[edge.Id] = edge;
        
        if (!_outgoingEdges.ContainsKey(edge.FromNodeId))
            _outgoingEdges[edge.FromNodeId] = new List<ContentEdge>();
        if (!_incomingEdges.ContainsKey(edge.ToNodeId))
            _incomingEdges[edge.ToNodeId] = new List<ContentEdge>();

        _outgoingEdges[edge.FromNodeId].Add(edge);
        _incomingEdges[edge.ToNodeId].Add(edge);
    }

    /// <summary>
    /// Updates an existing node (for content changes/versions).
    /// </summary>
    public void UpdateNode(ContentNode updatedNode)
    {
        ArgumentNullException.ThrowIfNull(updatedNode);
        
        if (!_nodes.ContainsKey(updatedNode.Id))
            throw new InvalidOperationException($"Node {updatedNode.Id} does not exist.");

        _nodes[updatedNode.Id] = updatedNode;
    }

    /// <summary>
    /// Removes a node and all its edges from the graph.
    /// </summary>
    public void RemoveNode(Guid nodeId)
    {
        if (!_nodes.ContainsKey(nodeId))
            return;

        // Remove all edges connected to this node
        if (_outgoingEdges.TryGetValue(nodeId, out var outgoing))
        {
            foreach (var edge in outgoing.ToList())
            {
                _edges.Remove(edge.Id);
                if (_incomingEdges.TryGetValue(edge.ToNodeId, out var incoming))
                    incoming.RemoveAll(e => e.Id == edge.Id);
            }
            _outgoingEdges.Remove(nodeId);
        }

        if (_incomingEdges.TryGetValue(nodeId, out var incomingEdges))
        {
            foreach (var edge in incomingEdges.ToList())
            {
                _edges.Remove(edge.Id);
                if (_outgoingEdges.TryGetValue(edge.FromNodeId, out var outgoingFrom))
                    outgoingFrom.RemoveAll(e => e.Id == edge.Id);
            }
            _incomingEdges.Remove(nodeId);
        }

        _nodes.Remove(nodeId);
    }

    /// <summary>
    /// Gets the count of nodes by type.
    /// </summary>
    public Dictionary<ContentNodeType, int> GetNodeCounts()
        => _nodes.Values
            .GroupBy(n => n.Type)
            .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>
    /// Checks if the graph contains a cycle starting from a concept (useful for prerequisite validation).
    /// </summary>
    public bool HasCycle(Guid startConceptId)
    {
        var visited = new HashSet<Guid>();
        var recursionStack = new HashSet<Guid>();
        return HasCycleHelper(startConceptId, visited, recursionStack);
    }

    private bool HasCycleHelper(Guid nodeId, HashSet<Guid> visited, HashSet<Guid> recursionStack)
    {
        if (recursionStack.Contains(nodeId))
            return true;
        if (visited.Contains(nodeId))
            return false;

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        if (_outgoingEdges.TryGetValue(nodeId, out var edges))
        {
            foreach (var edge in edges.Where(e => e.Type == ContentEdgeType.DependsOn))
            {
                if (HasCycleHelper(edge.ToNodeId, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(nodeId);
        return false;
    }
}
