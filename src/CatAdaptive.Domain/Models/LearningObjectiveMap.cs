using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Represents a learning objective extracted from slides.
/// </summary>
public sealed record LearningObjective(
    Guid Id,
    Guid SourceSlideId,
    string Text,
    BloomsLevel BloomsLevel,
    string Topic,
    IReadOnlyList<string> Keywords);

/// <summary>
/// Maps a learning objective to content that supports it.
/// </summary>
public sealed record ObjectiveContentMapping(
    Guid ObjectiveId,
    Guid ContentNodeId,
    ContentNodeType ContentType,
    double RelevanceScore,
    string Rationale);

/// <summary>
/// Domain node representing a concept in the knowledge domain.
/// </summary>
public sealed record DomainNode(
    Guid Id,
    string Title,
    string Description,
    DomainNodeType Type,
    BloomsLevel BloomsLevel,
    double Difficulty,
    double ExamRelevanceWeight,
    IReadOnlyList<string> Tags);

/// <summary>
/// Types of domain nodes.
/// </summary>
public enum DomainNodeType
{
    Concept,
    Skill,
    Objective,
    Topic,
    Subtopic
}

/// <summary>
/// Edge in the domain knowledge graph.
/// </summary>
public sealed record DomainEdge(
    Guid Id,
    Guid FromNodeId,
    Guid ToNodeId,
    DomainEdgeType Type,
    double Strength = 1.0);

/// <summary>
/// Types of domain edges.
/// </summary>
public enum DomainEdgeType
{
    PrerequisiteOf,
    PartOf,
    RelatedTo,
    ContrastsWith
}

/// <summary>
/// Domain Knowledge Graph representing the structure of knowledge.
/// </summary>
public sealed class DomainKnowledgeGraph
{
    private readonly Dictionary<Guid, DomainNode> _nodes = new();
    private readonly Dictionary<Guid, DomainEdge> _edges = new();
    private readonly Dictionary<Guid, List<DomainEdge>> _outgoingEdges = new();
    private readonly Dictionary<Guid, List<DomainEdge>> _incomingEdges = new();

    public IReadOnlyDictionary<Guid, DomainNode> Nodes 
        => new ReadOnlyDictionary<Guid, DomainNode>(_nodes);
    
    public IReadOnlyDictionary<Guid, DomainEdge> Edges 
        => new ReadOnlyDictionary<Guid, DomainEdge>(_edges);

    public void AddNode(DomainNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.Id] = node;

        if (!_outgoingEdges.ContainsKey(node.Id))
            _outgoingEdges[node.Id] = new List<DomainEdge>();
        if (!_incomingEdges.ContainsKey(node.Id))
            _incomingEdges[node.Id] = new List<DomainEdge>();
    }

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

    public DomainNode? GetNode(Guid id) => _nodes.GetValueOrDefault(id);

    public IReadOnlyList<DomainNode> GetNodesByType(DomainNodeType type)
        => _nodes.Values.Where(n => n.Type == type).ToList();

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

    public IReadOnlyList<DomainNode> GetRelatedNodes(Guid nodeId)
    {
        var related = new List<DomainNode>();

        if (_outgoingEdges.TryGetValue(nodeId, out var outgoing))
        {
            related.AddRange(outgoing
                .Where(e => e.Type == DomainEdgeType.RelatedTo)
                .Select(e => GetNode(e.ToNodeId))
                .Where(n => n is not null)
                .Cast<DomainNode>());
        }

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
}

/// <summary>
/// Maps learning objectives to content and domain nodes.
/// </summary>
public sealed class LearningObjectiveMap
{
    private readonly Dictionary<Guid, LearningObjective> _objectives = new();
    private readonly Dictionary<Guid, List<ObjectiveContentMapping>> _mappings = new();
    private readonly Dictionary<Guid, List<Guid>> _objectiveToDomainNodes = new();

    public IReadOnlyDictionary<Guid, LearningObjective> Objectives
        => new ReadOnlyDictionary<Guid, LearningObjective>(_objectives);

    public IReadOnlyDictionary<Guid, List<ObjectiveContentMapping>> Mappings
        => new ReadOnlyDictionary<Guid, List<ObjectiveContentMapping>>(_mappings);

    /// <summary>
    /// Adds a learning objective.
    /// </summary>
    public void AddObjective(LearningObjective objective)
    {
        ArgumentNullException.ThrowIfNull(objective);
        _objectives[objective.Id] = objective;
        
        if (!_mappings.ContainsKey(objective.Id))
            _mappings[objective.Id] = new List<ObjectiveContentMapping>();
        if (!_objectiveToDomainNodes.ContainsKey(objective.Id))
            _objectiveToDomainNodes[objective.Id] = new List<Guid>();
    }

    /// <summary>
    /// Maps a learning objective to content.
    /// </summary>
    public void AddMapping(Guid objectiveId, ObjectiveContentMapping mapping)
    {
        if (!_mappings.ContainsKey(objectiveId))
            _mappings[objectiveId] = new List<ObjectiveContentMapping>();
        
        _mappings[objectiveId].Add(mapping);
    }

    /// <summary>
    /// Maps a learning objective to a domain node.
    /// </summary>
    public void LinkToDomainNode(Guid objectiveId, Guid domainNodeId)
    {
        if (!_objectiveToDomainNodes.ContainsKey(objectiveId))
            _objectiveToDomainNodes[objectiveId] = new List<Guid>();
        
        if (!_objectiveToDomainNodes[objectiveId].Contains(domainNodeId))
            _objectiveToDomainNodes[objectiveId].Add(domainNodeId);
    }

    /// <summary>
    /// Gets an objective by ID.
    /// </summary>
    public LearningObjective? GetObjective(Guid id) => _objectives.GetValueOrDefault(id);

    /// <summary>
    /// Gets content mappings for an objective.
    /// </summary>
    public IReadOnlyList<ObjectiveContentMapping> GetMappingsForObjective(Guid objectiveId)
        => _mappings.TryGetValue(objectiveId, out var mappings) 
            ? mappings 
            : Array.Empty<ObjectiveContentMapping>();

    /// <summary>
    /// Gets domain nodes linked to an objective.
    /// </summary>
    public IReadOnlyList<Guid> GetDomainNodesForObjective(Guid objectiveId)
        => _objectiveToDomainNodes.TryGetValue(objectiveId, out var nodes)
            ? nodes
            : Array.Empty<Guid>();

    /// <summary>
    /// Gets objectives by topic.
    /// </summary>
    public IReadOnlyList<LearningObjective> GetObjectivesByTopic(string topic)
        => _objectives.Values
            .Where(o => o.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Gets objectives by Bloom's level.
    /// </summary>
    public IReadOnlyList<LearningObjective> GetObjectivesByBloomsLevel(BloomsLevel level)
        => _objectives.Values.Where(o => o.BloomsLevel == level).ToList();
}
