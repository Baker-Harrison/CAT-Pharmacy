namespace CatAdaptive.Application.Abstractions;

public sealed record LessonQuizAnswer(
    Guid QuestionId,
    string ResponseText);
