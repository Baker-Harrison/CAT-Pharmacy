using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Google.GenAI;

namespace CatAdaptive.Infrastructure.Generation;

public sealed class GeminiLessonPlanGenerator : ILessonPlanGenerator
{
    private readonly Client _client;
    private readonly string _modelName;

    public GeminiLessonPlanGenerator(string? apiKey = null, string modelName = "gemini-2.0-flash-exp")
    {
        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new Client()
            : new Client(apiKey: apiKey);
        _modelName = modelName;
    }

    public async Task<IReadOnlyList<LessonPlan>> GenerateInitialLessonsAsync(
        AIEnhancedContentGraph contentGraph,
        CancellationToken ct = default)
    {
        var lessons = new List<LessonPlan>();

        var explanationNodes = contentGraph.GetNodesByType(ContentNodeType.Explanation).Take(5);

        foreach (var node in explanationNodes)
        {
            var lesson = CreateLessonFromContentNode(node, isRemediation: false);
            lessons.Add(lesson);
        }

        return await Task.FromResult<IReadOnlyList<LessonPlan>>(lessons);
    }

    public async Task<IReadOnlyList<LessonPlan>> GenerateNextLessonsAsync(
        AIEnhancedContentGraph contentGraph,
        DomainKnowledgeGraph knowledgeGraph,
        IReadOnlyList<Guid> existingConceptIds,
        CancellationToken ct = default)
    {
        var lessons = new List<LessonPlan>();

        var uncoveredNodes = contentGraph.Nodes.Values
            .Where(n => n.Type == ContentNodeType.Explanation)
            .Where(n => !n.LinkedDomainNodes.Any(id => existingConceptIds.Contains(id)))
            .Take(3);

        foreach (var node in uncoveredNodes)
        {
            var lesson = CreateLessonFromContentNode(node, isRemediation: false);
            lessons.Add(lesson);
        }

        return await Task.FromResult<IReadOnlyList<LessonPlan>>(lessons);
    }

    public async Task<LessonPlan?> GenerateRemediationLessonAsync(
        AIEnhancedContentGraph contentGraph,
        DomainKnowledgeGraph knowledgeGraph,
        Guid conceptId,
        CancellationToken ct = default)
    {
        var simplerContent = contentGraph.GetSimplerContent(conceptId, ContentNodeType.Explanation, 0.5)
            .FirstOrDefault();

        if (simplerContent != null)
        {
            return await Task.FromResult(CreateLessonFromContentNode(simplerContent, isRemediation: true));
        }

        var anyContent = contentGraph.GetContentForDomainNodes(new[] { conceptId })
            .FirstOrDefault();

        if (anyContent != null)
        {
            return await Task.FromResult(CreateLessonFromContentNode(anyContent, isRemediation: true));
        }

        return null;
    }

    private LessonPlan CreateLessonFromContentNode(ContentNode node, bool isRemediation)
    {
        var conceptId = node.LinkedDomainNodes.FirstOrDefault();

        var section = new LessonSection(
            Heading: node.Title,
            Body: node.Content,
            Prompts: new List<LessonPrompt>
            {
                new("What are the key concepts covered in this section?", null)
            },
            Id: Guid.NewGuid());

        var rubric = EvaluationRubric.Create(
            requiredPoints: new[] { "Key concept understanding" },
            keyConcepts: node.Tags.ToList(),
            minExplanationQuality: 0.7);

        var question = new LessonQuizQuestion(
            Id: Guid.NewGuid(),
            ConceptId: conceptId,
            Type: LessonQuizQuestionType.OpenResponse,
            Prompt: $"Explain the main concepts of {node.Title} in your own words.",
            ExpectedAnswer: "A comprehensive explanation covering the key points.",
            Rubric: rubric);

        var quiz = new LessonQuiz(new[] { question });

        return LessonPlan.Create(
            conceptId: conceptId,
            title: node.Title,
            summary: node.Content.Length > 200 ? node.Content.Substring(0, 200) + "..." : node.Content,
            estimatedReadMinutes: node.EstimatedTimeMinutes,
            isRemediation: isRemediation,
            sections: new[] { section },
            quiz: quiz);
    }
}
