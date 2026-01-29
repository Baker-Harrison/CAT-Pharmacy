using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Abstractions;

public interface ILessonQuizEvaluator
{
    Task<LessonQuizResult> EvaluateAsync(
        LessonQuiz quiz,
        IReadOnlyList<LessonQuizAnswer> answers,
        CancellationToken ct = default);
}
