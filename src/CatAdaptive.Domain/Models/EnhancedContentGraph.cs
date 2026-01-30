using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Enhanced content node with additional metadata for adaptive learning.
/// </summary>
public sealed record EnhancedContentNode(
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
    ContentOrigin SourceOrigin = ContentOrigin.Internal);

/// <summary>
/// Enhanced edge in the Content Graph.
/// </summary>
public sealed record EnhancedContentEdge(
    Guid Id,
    Guid FromNodeId,
    Guid ToNodeId,
    ContentEdgeType Type,
    double Strength = 1.0);

/// <summary>
/// Enhanced Content Graph with comprehensive content repository for adaptive lessons.
/// </summary>
public sealed class EnhancedContentGraph
{
    private readonly Dictionary<Guid, EnhancedContentNode> _nodes = new();
    private readonly Dictionary<Guid, EnhancedContentEdge> _edges = new();
    private readonly Dictionary<Guid, List<EnhancedContentEdge>> _outgoingEdges = new();
    private readonly Dictionary<Guid, List<EnhancedContentEdge>> _incomingEdges = new();

    public IReadOnlyDictionary<Guid, EnhancedContentNode> Nodes => new ReadOnlyDictionary<Guid, EnhancedContentNode>(_nodes);
    public IReadOnlyDictionary<Guid, EnhancedContentEdge> Edges => new ReadOnlyDictionary<Guid, EnhancedContentEdge>(_edges);

    /// <summary>
    /// Adds a node to the content graph.
    /// </summary>
    public void AddNode(EnhancedContentNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.Id] = node;
        
        if (!_outgoingEdges.ContainsKey(node.Id))
            _outgoingEdges[node.Id] = new List<EnhancedContentEdge>();
        if (!_incomingEdges.ContainsKey(node.Id))
            _incomingEdges[node.Id] = new List<EnhancedContentEdge>();
    }

    /// <summary>
    /// Adds an edge to the content graph.
    /// </summary>
    public void AddEdge(EnhancedContentEdge edge)
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
    public EnhancedContentNode? GetNode(Guid id) => _nodes.GetValueOrDefault(id);

    /// <summary>
    /// Gets all nodes of a specific type.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetNodesByType(ContentNodeType type)
        => _nodes.Values.Where(n => n.Type == type).ToList();

    /// <summary>
    /// Gets all nodes of a specific modality.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetNodesByModality(ContentModality modality)
        => _nodes.Values.Where(n => n.Modality == modality).ToList();

    /// <summary>
    /// Gets content nodes linked to specific domain nodes.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetContentForDomainNodes(IEnumerable<Guid> domainNodeIds)
    {
        var nodeIdSet = domainNodeIds.ToHashSet();
        return _nodes.Values
            .Where(n => n.LinkedDomainNodes.Any(id => nodeIdSet.Contains(id)))
            .ToList();
    }

    /// <summary>
    /// Gets explanations for domain nodes.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetExplanationsForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => n.Type == ContentNodeType.Explanation && 
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .OrderBy(n => n.Difficulty)
            .ToList();

    /// <summary>
    /// Gets worked examples for domain nodes.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetExamplesForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => (n.Type == ContentNodeType.Example || n.Type == ContentNodeType.WorkedExample || n.Type == ContentNodeType.WorkedProblem) && 
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .OrderBy(n => n.Difficulty)
            .ToList();

    /// <summary>
    /// Gets clinical cases for domain nodes.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetCasesForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => n.Type == ContentNodeType.ClinicalCase && 
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .OrderBy(n => n.Difficulty)
            .ToList();

    /// <summary>
    /// Gets questions for domain nodes at specific Bloom's level.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetQuestionsForDomain(Guid domainNodeId, BloomsLevel? bloomsLevel = null)
    {
        var query = _nodes.Values
            .Where(n => n.Type == ContentNodeType.Question && 
                       n.LinkedDomainNodes.Contains(domainNodeId));
        
        if (bloomsLevel.HasValue)
            query = query.Where(n => n.BloomsLevel == bloomsLevel.Value);
        
        return query.OrderBy(n => n.Difficulty).ToList();
    }

    /// <summary>
    /// Gets visuals for domain nodes.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetVisualsForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => n.Type == ContentNodeType.Visual && 
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .ToList();

    /// <summary>
    /// Gets mnemonics for domain nodes.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetMnemonicsForDomain(Guid domainNodeId)
        => _nodes.Values
            .Where(n => n.Type == ContentNodeType.Mnemonic && 
                       n.LinkedDomainNodes.Contains(domainNodeId))
            .ToList();

    /// <summary>
    /// Gets alternative content when remediation is needed (different modality).
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetAlternativeContent(
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
    /// Gets content by tags.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetContentByTags(IEnumerable<string> tags)
    {
        var tagSet = tags.ToHashSet();
        return _nodes.Values
            .Where(n => n.Tags.Any(t => tagSet.Contains(t)))
            .ToList();
    }

    /// <summary>
    /// Gets simpler content for scaffolding.
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetSimplerContent(
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
    /// Gets content sorted by exam relevance (through domain nodes).
    /// </summary>
    public IReadOnlyList<EnhancedContentNode> GetContentByExamRelevance(
        DomainKnowledgeGraph domainGraph,
        int maxCount = 50)
    {
        return _nodes.Values
            .SelectMany(n => n.LinkedDomainNodes.Select(dnId => (Content: n, DomainNodeId: dnId)))
            .Select(tuple => (
                tuple.Content,
                Relevance: domainGraph.GetNode(tuple.DomainNodeId)?.ExamRelevanceWeight ?? 0.0))
            .OrderByDescending(x => x.Relevance)
            .ThenBy(x => x.Content.Difficulty)
            .Select(x => x.Content)
            .Take(maxCount)
            .Distinct()
            .ToList();
    }
}
