using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

public enum LessonQuizQuestionType
{
    FillInBlank,
    OpenResponse
}

public sealed record LessonPrompt(
    string Prompt,
    string? ExpectedAnswer);

public sealed record LessonSection(
    string Heading,
    string Body,
    IReadOnlyList<LessonPrompt> Prompts);

public sealed record LessonQuizQuestion(
    Guid Id,
    Guid ConceptId,
    LessonQuizQuestionType Type,
    string Prompt,
    string ExpectedAnswer,
    EvaluationRubric Rubric);

public sealed record LessonQuiz(IReadOnlyList<LessonQuizQuestion> Questions);

public sealed record LessonQuizQuestionResult(
    Guid QuestionId,
    double Score,
    bool IsCorrect,
    string? Feedback);

public sealed record LessonQuizResult(
    DateTimeOffset CompletedAt,
    double ScorePercent,
    IReadOnlyList<LessonQuizQuestionResult> QuestionResults);

public sealed record LessonPlan(
    Guid Id,
    Guid ConceptId,
    string Title,
    string Summary,
    int EstimatedReadMinutes,
    bool IsRemediation,
    IReadOnlyList<LessonSection> Sections,
    LessonQuiz Quiz,
    LessonQuizResult? QuizResult,
    double ProgressPercent,
    double? LastScorePercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static LessonPlan Create(
        Guid conceptId,
        string title,
        string summary,
        int estimatedReadMinutes,
        bool isRemediation,
        IEnumerable<LessonSection> sections,
        LessonQuiz quiz)
    {
        var sectionList = sections?.ToList() ?? new List<LessonSection>();

        return new LessonPlan(
            Guid.NewGuid(),
            conceptId,
            title.Trim(),
            summary.Trim(),
            Math.Clamp(estimatedReadMinutes, 1, 60),
            isRemediation,
            new ReadOnlyCollection<LessonSection>(sectionList),
            quiz,
            QuizResult: null,
            ProgressPercent: 0,
            LastScorePercent: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);
    }

    public LessonPlan WithQuizResult(LessonQuizResult result)
    {
        return this with
        {
            QuizResult = result,
            ProgressPercent = 100,
            LastScorePercent = result.ScorePercent,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
