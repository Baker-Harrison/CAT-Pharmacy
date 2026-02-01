using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Content node in the AI-enhanced content graph.
/// </summary>
public sealed record ContentNode(
    Guid Id,
    ContentNodeType Type,
    string Title,
    string Content,
    ContentModality Modality,
    BloomsLevel BloomsLevel,
    double Difficulty,
    int EstimatedTimeMinutes,
    IReadOnlyList<Guid> LinkedDomainNodes,
    IReadOnlyList<string> Tags,
    ContentOrigin SourceOrigin,
    double QualityScore,
    DateTimeOffset CreatedAt,
    string? SourceUrl = null);

/// <summary>
/// Edge in the AI-enhanced content graph.
/// </summary>
public sealed record ContentEdge(
    Guid Id,
    Guid FromNodeId,
    Guid ToNodeId,
    ContentEdgeType Type,
    double Strength = 1.0);

/// <summary>
/// AI-enhanced content graph with comprehensive content repository.
/// </summary>
public sealed class AIEnhancedContentGraph
{
    private readonly Dictionary<Guid, ContentNode> _nodes = new();
    private readonly Dictionary<Guid, ContentEdge> _edges = new();
    private readonly Dictionary<Guid, List<ContentEdge>> _outgoingEdges = new();
    private readonly Dictionary<Guid, List<ContentEdge>> _incomingEdges = new();

    public IReadOnlyDictionary<Guid, ContentNode> Nodes 
        => new ReadOnlyDictionary<Guid, ContentNode>(_nodes);
    
    public IReadOnlyDictionary<Guid, ContentEdge> Edges 
        => new ReadOnlyDictionary<Guid, ContentEdge>(_edges);

    /// <summary>
    /// Adds a node to the content graph.
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
    /// Adds an edge to the content graph.
    /// </summary>
    public void AddEdge(ContentEdge edge)
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
    public ContentNode? GetNode(Guid id) => _nodes.GetValueOrDefault(id);

    /// <summary>
    /// Gets all nodes of a specific type.
    /// </summary>
    public IReadOnlyList<ContentNode> GetNodesByType(ContentNodeType type)
        => _nodes.Values.Where(n => n.Type == type).ToList();

    /// <summary>
    /// Gets all nodes of a specific modality.
    /// </summary>
    public IReadOnlyList<ContentNode> GetNodesByModality(ContentModality modality)
        => _nodes.Values.Where(n => n.Modality == modality).ToList();

    /// <summary>
    /// Gets content nodes linked to specific domain nodes.
    /// </summary>
    public IReadOnlyList<ContentNode> GetContentForDomainNodes(IEnumerable<Guid> domainNodeIds)
    {
        var nodeIdSet = domainNodeIds.ToHashSet();
        return _nodes.Values
            .Where(n => n.LinkedDomainNodes.Any(id => nodeIdSet.Contains(id)))
            .ToList();
    }

    /// <summary>
    /// Gets visual content for a domain node.
    /// </summary>
    public IEnumerable<ContentNode> GetVisualContent(Guid domainNodeId)
        => _nodes.Values.Where(n => 
            n.LinkedDomainNodes.Contains(domainNodeId) && 
            n.Modality == ContentModality.Visual);

    /// <summary>
    /// Gets interactive content for a domain node.
    /// </summary>
    public IEnumerable<ContentNode> GetInteractiveContent(Guid domainNodeId)
        => _nodes.Values.Where(n => 
            n.LinkedDomainNodes.Contains(domainNodeId) && 
            n.Type == ContentNodeType.Interactive);

    /// <summary>
    /// Gets high quality content for a domain node.
    /// </summary>
    public IEnumerable<ContentNode> GetHighQualityContent(Guid domainNodeId, double minQuality = 0.8)
        => _nodes.Values.Where(n => 
            n.LinkedDomainNodes.Contains(domainNodeId) && 
            n.QualityScore >= minQuality);

    /// <summary>
    /// Gets explanations for a domain node.
    /// </summary>
    public IReadOnlyList<ContentNode> GetExplanationsForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => n.Type == ContentNodeType.Explanation &&
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .OrderBy(n => n.Difficulty)
            .ToList();

    /// <summary>
    /// Gets worked examples for a domain node.
    /// </summary>
    public IReadOnlyList<ContentNode> GetExamplesForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => (n.Type == ContentNodeType.Example || 
                        n.Type == ContentNodeType.WorkedExample || 
                        n.Type == ContentNodeType.WorkedProblem) &&
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .OrderBy(n => n.Difficulty)
            .ToList();

    /// <summary>
    /// Gets clinical cases for a domain node.
    /// </summary>
    public IReadOnlyList<ContentNode> GetCasesForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => n.Type == ContentNodeType.ClinicalCase &&
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .OrderBy(n => n.Difficulty)
            .ToList();

    /// <summary>
    /// Gets questions for a domain node at a specific Bloom's level.
    /// </summary>
    public IReadOnlyList<ContentNode> GetQuestionsForDomain(Guid domainNodeId, BloomsLevel? bloomsLevel = null)
    {
        var query = _nodes.Values
            .Where(n => n.Type == ContentNodeType.Question &&
                       n.LinkedDomainNodes.Contains(domainNodeId));

        if (bloomsLevel.HasValue)
            query = query.Where(n => n.BloomsLevel == bloomsLevel.Value);

        return query.OrderBy(n => n.Difficulty).ToList();
    }

    /// <summary>
    /// Gets mnemonics for a domain node.
    /// </summary>
    public IReadOnlyList<ContentNode> GetMnemonicsForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => n.Type == ContentNodeType.Mnemonic &&
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .ToList();

    /// <summary>
    /// Gets alternative content with different modality.
    /// </summary>
    public IReadOnlyList<ContentNode> GetAlternativeContent(
        Guid domainNodeId,
        ContentModality currentModality,
        ContentNodeType contentType)
    {
        return _nodes.Values
            .Where(n => n.Type == contentType &&
                       n.LinkedDomainNodes.Contains(domainNodeId) &&
                       n.Modality != currentModality)
            .OrderBy(n => n.Difficulty)
            .ToList();
    }

    /// <summary>
    /// Gets simpler content for scaffolding.
    /// </summary>
    public IReadOnlyList<ContentNode> GetSimplerContent(
        Guid domainNodeId,
        ContentNodeType contentType,
        double maxDifficulty)
    {
        return _nodes.Values
            .Where(n => n.Type == contentType &&
                       n.LinkedDomainNodes.Contains(domainNodeId) &&
                       n.Difficulty <= maxDifficulty)
            .OrderBy(n => n.Difficulty)
            .ToList();
    }

    /// <summary>
    /// Gets content by tags.
    /// </summary>
    public IReadOnlyList<ContentNode> GetContentByTags(IEnumerable<string> tags)
    {
        var tagSet = tags.ToHashSet();
        return _nodes.Values
            .Where(n => n.Tags.Any(t => tagSet.Contains(t)))
            .ToList();
    }

    /// <summary>
    /// Gets content by origin.
    /// </summary>
    public IReadOnlyList<ContentNode> GetContentByOrigin(ContentOrigin origin)
        => _nodes.Values.Where(n => n.SourceOrigin == origin).ToList();

    /// <summary>
    /// Gets content sorted by quality score.
    /// </summary>
    public IReadOnlyList<ContentNode> GetContentByQuality(int maxCount = 50)
        => _nodes.Values
            .OrderByDescending(n => n.QualityScore)
            .Take(maxCount)
            .ToList();
}
