using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

/// <summary>
/// Represents the state of an adaptive learning session.
/// </summary>
public enum AdaptiveSessionState
{
    NotStarted,
    PreQuiz,
    Lesson,
    PostQuiz,
    Results,
    Completed
}

/// <summary>
/// Target node selected for the current lesson.
/// </summary>
public sealed record TargetNode(
    Guid DomainNodeId,
    string Title,
    double Priority,
    string Rationale);

/// <summary>
/// Diagnostic pre-quiz for assessing current mastery.
/// </summary>
public sealed record DiagnosticPreQuiz(
    Guid Id,
    Guid TargetNodeId,
    IReadOnlyList<DiagnosticQuestion> Questions,
    DateTimeOffset CreatedAt);

/// <summary>
/// Question in a diagnostic quiz.
/// </summary>
public sealed record DiagnosticQuestion(
    Guid Id,
    Guid DomainNodeId,
    string Prompt,
    PromptFormat Format,
    BloomsLevel BloomsLevel,
    string ExpectedAnswer);

/// <summary>
/// Generated adaptive lesson.
/// </summary>
public sealed record AdaptiveLesson(
    Guid Id,
    Guid TargetNodeId,
    string Title,
    string Summary,
    IReadOnlyList<LessonComponent> Components,
    int EstimatedTimeMinutes,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Component of an adaptive lesson.
/// </summary>
public sealed record LessonComponent(
    Guid Id,
    LessonComponentType Type,
    string Title,
    string Content,
    ContentModality Modality,
    int OrderIndex);

/// <summary>
/// Types of lesson components.
/// </summary>
public enum LessonComponentType
{
    PredictionPrompt,
    FocusedExplanation,
    WorkedExample,
    ActiveGenerationTask,
    VisualAid,
    MnemonicDevice,
    ClinicalCase
}

/// <summary>
/// Results of an adaptive session.
/// </summary>
public sealed record AdaptiveSessionResult(
    Guid SessionId,
    Guid TargetNodeId,
    DiagnosticQuizResult? PreQuizResult,
    LessonQuizResult? PostQuizResult,
    double LearningGain,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<Guid> RecommendedNextNodes,
    DateTimeOffset CompletedAt);

/// <summary>
/// Result of a diagnostic quiz.
/// </summary>
public sealed record DiagnosticQuizResult(
    Guid QuizId,
    double ScorePercent,
    IReadOnlyList<DiagnosticQuestionResult> QuestionResults,
    DateTimeOffset CompletedAt);

/// <summary>
/// Result of a single diagnostic question.
/// </summary>
public sealed record DiagnosticQuestionResult(
    Guid QuestionId,
    bool IsCorrect,
    double Score,
    string? Feedback,
    ErrorType ErrorType,
    double Confidence);

/// <summary>
/// Adaptive Session - manages the closed adaptive loop for learning.
/// </summary>
public sealed class AdaptiveSession
{
    private readonly List<Guid> _recommendedNextNodes = new();

    public Guid Id { get; }
    public Guid LearnerId { get; }
    public AdaptiveSessionState State { get; private set; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public TargetNode? TargetNode { get; private set; }
    public DiagnosticPreQuiz? PreQuiz { get; private set; }
    public AdaptiveLesson? Lesson { get; private set; }
    public AdaptiveSessionResult? Result { get; private set; }

    public IReadOnlyList<Guid> RecommendedNextNodes => new ReadOnlyCollection<Guid>(_recommendedNextNodes);

    public AdaptiveSession(Guid learnerId)
    {
        Id = Guid.NewGuid();
        LearnerId = learnerId;
        State = AdaptiveSessionState.NotStarted;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Starts the session with a selected target node.
    /// </summary>
    public void StartWithTarget(TargetNode targetNode)
    {
        if (State != AdaptiveSessionState.NotStarted)
            throw new InvalidOperationException($"Session is already in state {State}");

        TargetNode = targetNode;
        State = AdaptiveSessionState.PreQuiz;
    }

    /// <summary>
    /// Sets the diagnostic pre-quiz.
    /// </summary>
    public void SetPreQuiz(DiagnosticPreQuiz preQuiz)
    {
        if (State != AdaptiveSessionState.PreQuiz)
            throw new InvalidOperationException($"Cannot set pre-quiz in state {State}");

        PreQuiz = preQuiz;
    }

    /// <summary>
    /// Completes the pre-quiz and moves to lesson phase.
    /// </summary>
    public void CompletePreQuiz()
    {
        if (State != AdaptiveSessionState.PreQuiz)
            throw new InvalidOperationException($"Cannot complete pre-quiz in state {State}");

        State = AdaptiveSessionState.Lesson;
    }

    /// <summary>
    /// Sets the generated lesson.
    /// </summary>
    public void SetLesson(AdaptiveLesson lesson)
    {
        if (State != AdaptiveSessionState.Lesson)
            throw new InvalidOperationException($"Cannot set lesson in state {State}");

        Lesson = lesson;
    }

    /// <summary>
    /// Completes the lesson and moves to post-quiz phase.
    /// </summary>
    public void CompleteLesson()
    {
        if (State != AdaptiveSessionState.Lesson)
            throw new InvalidOperationException($"Cannot complete lesson in state {State}");

        State = AdaptiveSessionState.PostQuiz;
    }

    /// <summary>
    /// Completes the session with results.
    /// </summary>
    public void CompleteWithResult(AdaptiveSessionResult result)
    {
        if (State != AdaptiveSessionState.PostQuiz && State != AdaptiveSessionState.Results)
            throw new InvalidOperationException($"Cannot complete session in state {State}");

        Result = result;
        State = AdaptiveSessionState.Completed;
        CompletedAt = DateTimeOffset.UtcNow;

        _recommendedNextNodes.Clear();
        foreach (var nodeId in result.RecommendedNextNodes)
        {
            _recommendedNextNodes.Add(nodeId);
        }
    }

    /// <summary>
    /// Moves to results screen after post-quiz.
    /// </summary>
    public void ShowResults()
    {
        if (State != AdaptiveSessionState.PostQuiz)
            throw new InvalidOperationException($"Cannot show results in state {State}");

        State = AdaptiveSessionState.Results;
    }

    /// <summary>
    /// Gets the duration of the session.
    /// </summary>
    public TimeSpan GetDuration()
    {
        var end = CompletedAt ?? DateTimeOffset.UtcNow;
        return end - StartedAt;
    }

    /// <summary>
    /// Checks if the session can continue to next lesson.
    /// </summary>
    public bool CanContinue()
    {
        return State == AdaptiveSessionState.Completed && _recommendedNextNodes.Count > 0;
    }
}
