using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Services;

public static class ContentGraphBuilder
{
    public static ContentGraph BuildFromKnowledgeUnits(IReadOnlyList<KnowledgeUnit> knowledgeUnits)
    {
        var graph = new ContentGraph();
        var conceptsByTopic = new Dictionary<string, ContentNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var unit in knowledgeUnits)
        {
            if (string.IsNullOrWhiteSpace(unit.Topic))
            {
                continue;
            }

            if (!conceptsByTopic.TryGetValue(unit.Topic, out var conceptNode))
            {
                conceptNode = ContentNode.CreateInternal(
                    ContentNodeType.Concept,
                    unit.Topic,
                    unit.SourceSlideId,
                    confidence: 0.9);

                conceptsByTopic[unit.Topic] = conceptNode;
                graph.AddNode(conceptNode);
            }

            AddExplanation(graph, conceptNode, unit.Summary, unit.SourceSlideId, confidence: 0.8);

            foreach (var keyPoint in unit.KeyPoints)
            {
                AddExplanation(graph, conceptNode, keyPoint, unit.SourceSlideId, confidence: 0.75);
            }

            foreach (var objective in unit.LearningObjectives)
            {
                AddLearningObjective(graph, conceptNode, objective, unit.SourceSlideId);
            }
        }

        return graph;
    }

    private static void AddExplanation(
        ContentGraph graph,
        ContentNode conceptNode,
        string? text,
        string sourceSlideId,
        double confidence)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var explanationNode = ContentNode.CreateInternal(
            ContentNodeType.Explanation,
            text,
            sourceSlideId,
            confidence: confidence);

        graph.AddNode(explanationNode);
        graph.AddEdge(ContentEdge.Explains(explanationNode.Id, conceptNode.Id));
    }

    private static void AddLearningObjective(
        ContentGraph graph,
        ContentNode conceptNode,
        string? text,
        string sourceSlideId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var objectiveNode = ContentNode.CreateInternal(
            ContentNodeType.LearningObjective,
            text,
            sourceSlideId,
            confidence: 0.7);

        graph.AddNode(objectiveNode);
        graph.AddEdge(ContentEdge.Explains(objectiveNode.Id, conceptNode.Id));
    }
}
