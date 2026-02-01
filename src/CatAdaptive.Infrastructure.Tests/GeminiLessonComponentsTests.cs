using System.Reflection;
using CatAdaptive.Domain.Models;
using CatAdaptive.Infrastructure.Generation;
using FluentAssertions;

namespace CatAdaptive.Infrastructure.Tests;

public sealed class GeminiLessonComponentsTests
{
    [Fact]
    public async Task GeminiLessonPlanGenerator_GeneratesInitialLessons()
    {
        var graph = new AIEnhancedContentGraph();
        var nodeId = Guid.NewGuid();
        graph.AddNode(new ContentNode(
            Guid.NewGuid(),
            ContentNodeType.Explanation,
            "Title",
            "Content",
            ContentModality.Text,
            BloomsLevel.Understand,
            0.2,
            5,
            new[] { nodeId },
            new[] { "tag" },
            ContentOrigin.Slides,
            0.9,
            DateTimeOffset.UtcNow));

        var generator = new GeminiLessonPlanGenerator();
        var lessons = await generator.GenerateInitialLessonsAsync(graph);

        lessons.Should().NotBeEmpty();
        lessons[0].Title.Should().Be("Title");
    }

    [Fact]
    public void GeminiLessonQuizEvaluator_ParseResults_ReturnsFallbackOnInvalid()
    {
        var method = typeof(GeminiLessonQuizEvaluator)
            .GetMethod("ParseResults", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var quiz = BuildQuiz();
        var results = (IReadOnlyList<LessonQuizQuestionResult>)method!
            .Invoke(null, new object?[] { "not-json", quiz.Questions })!;

        results.Should().HaveCount(quiz.Questions.Count);
        results.Should().OnlyContain(r => r.Score == 0 && !r.IsCorrect);
    }

    [Fact]
    public void GeminiLessonQuizEvaluator_ParseResults_MapsResultsWhenJsonProvided()
    {
        var method = typeof(GeminiLessonQuizEvaluator)
            .GetMethod("ParseResults", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var quiz = BuildQuiz();
        var json = $"[{{\"questionId\":\"{quiz.Questions[0].Id}\",\"score\":0.9,\"isCorrect\":true,\"feedback\":\"ok\"}}]";

        var results = (IReadOnlyList<LessonQuizQuestionResult>)method!
            .Invoke(null, new object?[] { json, quiz.Questions })!;

        results.Should().HaveCount(quiz.Questions.Count);
        results[0].Score.Should().Be(0.9);
        results[0].IsCorrect.Should().BeTrue();
    }

    private static LessonQuiz BuildQuiz()
    {
        var rubric = EvaluationRubric.Create(new[] { "point" }, Array.Empty<string>(), Array.Empty<string>(), 0.7);
        var question = new LessonQuizQuestion(Guid.NewGuid(), Guid.NewGuid(), LessonQuizQuestionType.OpenResponse, "Prompt", "Expected", rubric);
        return new LessonQuiz(new List<LessonQuizQuestion> { question });
    }
}
