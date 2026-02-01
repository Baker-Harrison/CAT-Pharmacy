using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Generation;

public sealed class GeminiLessonPlanGenerator : ILessonPlanGenerator
{
    private const int DefaultLessonCount = 5;
    private const int DefaultQuizQuestionCount = 5;

    public GeminiLessonPlanGenerator(string? apiKey = null, string modelName = "gemini-2.0-flash-exp")
    {
    }

    public async Task<IReadOnlyList<LessonPlan>> GenerateInitialLessonsAsync(
        AIEnhancedContentGraph contentGraph,
        CancellationToken ct = default)
    {
        var lessons = SelectInitialNodes(contentGraph)
            .Select(node => CreateLessonFromContentNode(node, null, isRemediation: false))
            .ToList();

        return await Task.FromResult<IReadOnlyList<LessonPlan>>(lessons);
    }

    public async Task<IReadOnlyList<LessonPlan>> GenerateNextLessonsAsync(
        AIEnhancedContentGraph contentGraph,
        DomainKnowledgeGraph knowledgeGraph,
        IReadOnlyList<Guid> existingConceptIds,
        CancellationToken ct = default)
    {
        var uncoveredNodes = contentGraph.Nodes.Values
            .Where(n => n.Type == ContentNodeType.Explanation)
            .Where(n => !n.LinkedDomainNodes.Any(id => existingConceptIds.Contains(id)))
            .OrderBy(n => n.Difficulty)
            .ThenByDescending(n => n.QualityScore)
            .Take(DefaultLessonCount)
            .ToList();

        if (uncoveredNodes.Count == 0)
        {
            uncoveredNodes = contentGraph.GetContentByQuality(DefaultLessonCount)
                .Where(n => n.Type == ContentNodeType.Explanation)
                .ToList();
        }

        var lessons = uncoveredNodes
            .Select(node => CreateLessonFromContentNode(node, knowledgeGraph, isRemediation: false))
            .ToList();

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
            return await Task.FromResult(CreateLessonFromContentNode(simplerContent, knowledgeGraph, isRemediation: true));
        }

        var anyContent = contentGraph.GetContentForDomainNodes(new[] { conceptId })
            .FirstOrDefault();

        return anyContent != null
            ? await Task.FromResult(CreateLessonFromContentNode(anyContent, knowledgeGraph, isRemediation: true))
            : null;
    }

    private static IReadOnlyList<ContentNode> SelectInitialNodes(AIEnhancedContentGraph contentGraph)
    {
        var explanationNodes = contentGraph.GetNodesByType(ContentNodeType.Explanation)
            .OrderBy(n => n.Difficulty)
            .ThenByDescending(n => n.QualityScore)
            .Take(DefaultLessonCount)
            .ToList();

        return explanationNodes.Count > 0
            ? explanationNodes
            : contentGraph.GetContentByQuality(DefaultLessonCount)
                .Where(n => n.Type == ContentNodeType.Explanation)
                .ToList();
    }

    private static LessonPlan CreateLessonFromContentNode(
        ContentNode node,
        DomainKnowledgeGraph? domainGraph,
        bool isRemediation)
    {
        var conceptId = ResolveConceptId(node, domainGraph);
        var domainNode = domainGraph?.GetNode(conceptId);

        var title = domainNode?.Title ?? node.Title;
        var summary = Truncate(node.Content, 240);
        var sections = BuildSections(node, title, isRemediation);
        var quiz = BuildQuiz(node, conceptId, title);

        return LessonPlan.Create(
            conceptId: conceptId,
            title: title,
            summary: summary,
            estimatedReadMinutes: node.EstimatedTimeMinutes,
            isRemediation: isRemediation,
            sections: sections,
            quiz: quiz);
    }

    private static Guid ResolveConceptId(ContentNode node, DomainKnowledgeGraph? domainGraph)
    {
        var linked = node.LinkedDomainNodes.FirstOrDefault(id => domainGraph?.GetNode(id) != null);
        if (linked != Guid.Empty)
        {
            return linked;
        }

        return node.LinkedDomainNodes.FirstOrDefault() != Guid.Empty
            ? node.LinkedDomainNodes.First()
            : node.Id;
    }

    private static IReadOnlyList<LessonSection> BuildSections(ContentNode node, string title, bool isRemediation)
    {
        var sections = new List<LessonSection>
        {
            new(
                "Retrieval Warm-Up",
                $"Before reading, retrieve what you already know about {title}.",
                new List<LessonPrompt>
                {
                    new($"From memory, define {title} in one sentence.", null),
                    new("List two related ideas you expect to see in this lesson.", null)
                },
                Guid.NewGuid())
        };

        var corePrompts = new List<LessonPrompt>
        {
            new($"Explain the core idea of {title} in your own words.", null),
            new("Identify one misconception that could lead to a wrong answer and correct it.", null)
        };

        var tagPrompt = node.Tags.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tagPrompt))
        {
            corePrompts.Add(new LessonPrompt(
                $"Connect the concept \"{tagPrompt}\" to {title} with a concrete example.",
                null));
        }

        if (isRemediation)
        {
            corePrompts.Add(new LessonPrompt(
                "Work through a simplified example step-by-step and explain each choice.",
                null));
        }

        sections.Add(new LessonSection(
            node.Title,
            node.Content,
            corePrompts,
            Guid.NewGuid()));

        sections.Add(new LessonSection(
            "Active Recall and Spaced Review",
            $"Use spaced repetition to strengthen your memory of {title}.",
            new List<LessonPrompt>
            {
                new("Without notes, list three key points from this lesson.", null),
                new("In 24 hours, write a short summary from memory and note any gaps.", null)
            },
            Guid.NewGuid()));

        return sections;
    }

    private static LessonQuiz BuildQuiz(ContentNode node, Guid conceptId, string title)
    {
        var tags = node.Tags.Take(3).ToList();
        var requiredPoints = tags.Count > 0
            ? tags.Select(t => $"Include {t}")
            : new[] { $"Describe the main idea of {title}" };

        var rubric = EvaluationRubric.Create(
            requiredPoints: requiredPoints,
            keyConcepts: tags,
            commonMisconceptions: Array.Empty<string>(),
            minExplanationQuality: 0.7);

        var questions = new List<LessonQuizQuestion>
        {
            new(
                Guid.NewGuid(),
                conceptId,
                LessonQuizQuestionType.FillInBlank,
                $"The core idea of {title} is _____.",
                tags.FirstOrDefault() ?? title,
                rubric),
            new(
                Guid.NewGuid(),
                conceptId,
                LessonQuizQuestionType.OpenResponse,
                $"Explain {title} in your own words, including an example.",
                tags.Count > 0 ? string.Join(", ", tags) : $"A clear explanation of {title}.",
                rubric),
            new(
                Guid.NewGuid(),
                conceptId,
                LessonQuizQuestionType.OpenResponse,
                $"What common mistake do learners make with {title}, and how do you avoid it?",
                tags.Count > 0 ? $"Misconceptions about {string.Join(", ", tags)}." : "A corrected misconception.",
                rubric)
        };

        if (questions.Count < DefaultQuizQuestionCount)
        {
            questions.Add(new LessonQuizQuestion(
                Guid.NewGuid(),
                conceptId,
                LessonQuizQuestionType.OpenResponse,
                $"Apply {title} to a new scenario or problem.",
                "A correct application that uses the lesson concepts.",
                rubric));
        }

        return new LessonQuiz(questions);
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim().Replace("\n", " ").Replace("\r", " ");
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
    }
}
