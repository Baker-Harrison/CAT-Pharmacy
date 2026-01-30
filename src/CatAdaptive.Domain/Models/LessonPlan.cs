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
    IReadOnlyList<LessonPrompt> Prompts,
    Guid Id);

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

public sealed record SectionProgress(
    Guid SectionId,
    bool IsRead,
    double ReadPercent,
    DateTimeOffset? LastReadAt);

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
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SectionProgress> SectionProgresses)
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
        var sectionProgresses = sectionList.Select(s => new SectionProgress(
            s.Id, // Use the section's actual ID
            false,
            0.0,
            null)).ToList();

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
            UpdatedAt: DateTimeOffset.UtcNow,
            SectionProgresses: new ReadOnlyCollection<SectionProgress>(sectionProgresses));
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

    public LessonPlan WithSectionProgress(Guid sectionId, double readPercent, bool isRead)
    {
        var updatedProgresses = SectionProgresses
            .Select(sp => sp.SectionId == sectionId 
                ? sp with { ReadPercent = Math.Clamp(readPercent, 0, 100), IsRead = isRead, LastReadAt = DateTimeOffset.UtcNow }
                : sp)
            .ToList();

        var overallProgress = updatedProgresses.Count > 0 
            ? updatedProgresses.Average(sp => sp.ReadPercent) 
            : 0;

        return this with
        {
            SectionProgresses = new ReadOnlyCollection<SectionProgress>(updatedProgresses),
            ProgressPercent = overallProgress,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
