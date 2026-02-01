using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Orchestrates personalized learning experiences.
/// </summary>
public sealed class PersonalizedLearningOrchestrator
{
    private readonly StudentStateService _studentStateService;
    private readonly AIContentExpansionService _contentExpander;
    private readonly ToTContentGenerator _contentGenerator;
    private readonly IAIContentGraphRepository _contentGraphRepository;
    private readonly ILearningObjectiveMapRepository _objectiveMapRepository;
    private readonly IDomainGraphRepository _domainGraphRepository;
    private readonly IGeminiService _gemini;
    private readonly ILogger<PersonalizedLearningOrchestrator> _logger;

    public PersonalizedLearningOrchestrator(
        StudentStateService studentStateService,
        AIContentExpansionService contentExpander,
        ToTContentGenerator contentGenerator,
        IAIContentGraphRepository contentGraphRepository,
        ILearningObjectiveMapRepository objectiveMapRepository,
        IDomainGraphRepository domainGraphRepository,
        IGeminiService gemini,
        ILogger<PersonalizedLearningOrchestrator> logger)
    {
        _studentStateService = studentStateService;
        _contentExpander = contentExpander;
        _contentGenerator = contentGenerator;
        _contentGraphRepository = contentGraphRepository;
        _objectiveMapRepository = objectiveMapRepository;
        _domainGraphRepository = domainGraphRepository;
        _gemini = gemini;
        _logger = logger;
    }

    /// <summary>
    /// Starts a personalized learning session.
    /// </summary>
    public async Task<LearningSession> StartPersonalizedSessionAsync(
        Guid studentId,
        LearningGoals goals,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting personalized session for student {StudentId}", studentId);

        // 1. Get or create student state
        var studentState = await _studentStateService.GetOrCreateStudentStateAsync(studentId, ct);

        // 2. Analyze knowledge and create path
        var analysis = await _studentStateService.AnalyzeKnowledgeStateAsync(studentState, ct);
        studentState.CurrentAnalysis = analysis;

        // 3. Generate personalized learning path
        var learningPath = await GeneratePersonalizedPathAsync(studentState, goals, ct);

        // 4. Create session
        var session = new LearningSession(
            SessionId: Guid.NewGuid(),
            StudentId: studentId,
            LearningPath: learningPath,
            StartTime: DateTimeOffset.UtcNow,
            Status: SessionStatus.Active);

        _logger.LogInformation(
            "Created session {SessionId} with {ModuleCount} modules",
            session.SessionId, learningPath.Modules.Count);

        return session;
    }

    /// <summary>
    /// Generates a personalized learning path based on student state and goals.
    /// </summary>
    private async Task<LearningPath> GeneratePersonalizedPathAsync(
        StudentStateModel studentState,
        LearningGoals goals,
        CancellationToken ct)
    {
        var modules = new List<LearningModule>();
        var domainGraph = await _domainGraphRepository.GetAsync(ct);
        var contentGraph = await _contentGraphRepository.GetDefaultAsync(ct);
        var objectiveMap = await _objectiveMapRepository.GetDefaultAsync(ct);

        // Get recommended nodes based on analysis
        var targetNodes = studentState.CurrentAnalysis.RecommendedNextNodes.Take(goals.MaxModules);

        foreach (var nodeId in targetNodes)
        {
            var node = domainGraph?.GetNode(nodeId);
            if (node == null) continue;

            // Find relevant objectives for this node
            var objectives = objectiveMap?.Objectives.Values
                .Where(o => objectiveMap.GetDomainNodesForObjective(o.Id).Contains(nodeId))
                .ToList() ?? new List<LearningObjective>();

            // Get content for this node
            var content = contentGraph?.GetContentForDomainNodes(new[] { nodeId }) 
                         ?? Array.Empty<ContentNode>();

            var module = new LearningModule(
                ModuleId: Guid.NewGuid(),
                DomainNodeId: nodeId,
                Title: node.Title,
                Description: node.Description,
                Objectives: objectives,
                Content: content.ToList(),
                EstimatedMinutes: content.Sum(c => c.EstimatedTimeMinutes),
                Difficulty: node.Difficulty,
                Status: ModuleStatus.NotStarted);

            modules.Add(module);
        }

        return new LearningPath(
            PathId: Guid.NewGuid(),
            Modules: modules,
            CurrentModuleIndex: 0,
            CreatedAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Gets the current module content personalized for the student.
    /// </summary>
    public async Task<PersonalizedModuleContent> GetCurrentModuleContentAsync(
        LearningSession session,
        CancellationToken ct = default)
    {
        var studentState = await _studentStateService.GetOrCreateStudentStateAsync(session.StudentId, ct);
        var currentModule = session.LearningPath.GetCurrentModule();

        if (currentModule == null)
        {
            throw new InvalidOperationException("No current module available");
        }

        // Generate personalized content for the module
        var personalizedContent = new List<PersonalizedContent>();

        foreach (var objective in currentModule.Objectives)
        {
            var request = new ContentGenerationRequest(
                ObjectiveId: objective.Id,
                Objective: objective.Text,
                ContentType: "explanation",
                TargetLevel: objective.BloomsLevel);

            var content = await _contentGenerator.GenerateWithToTAsync(request, studentState, ct);
            personalizedContent.Add(content);
        }

        // Get assessment questions
        var contentGraph = await _contentGraphRepository.GetDefaultAsync(ct);
        var questions = contentGraph?.GetQuestionsForDomain(currentModule.DomainNodeId)
            .Take(5).ToList() ?? new List<ContentNode>();

        return new PersonalizedModuleContent(
            ModuleId: currentModule.ModuleId,
            Title: currentModule.Title,
            PersonalizedContent: personalizedContent,
            AssessmentQuestions: questions,
            EstimatedMinutes: currentModule.EstimatedMinutes,
            StudentMastery: studentState.GetKnowledgeMastery(currentModule.DomainNodeId));
    }

    /// <summary>
    /// Processes a student's response and adapts content.
    /// </summary>
    public async Task<AdaptiveResponse> ProcessInteractionAsync(
        LearningSession session,
        StudentInteraction interaction,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing interaction for session {SessionId}, node {NodeId}",
            session.SessionId, interaction.NodeId);

        // Create mastery event from interaction
        var masteryEvent = new MasteryEvent(
            NodeId: interaction.NodeId,
            Type: MasteryEventType.PracticeAttempt,
            Strength: interaction.CorrectnesScore,
            Evidence: interaction.Response,
            Context: new AssessmentContext(
                Difficulty: interaction.Difficulty,
                BloomsLevel: interaction.BloomsLevel,
                ContentType: interaction.ContentType,
                AttemptNumber: interaction.AttemptNumber),
            Timestamp: DateTimeOffset.UtcNow,
            Confidence: interaction.Confidence);

        // Update student state
        var updatedState = await _studentStateService.UpdateMasteryAsync(
            session.StudentId, masteryEvent, ct);

        // Generate adaptive response
        var adaptiveContent = await GenerateAdaptiveContentAsync(
            interaction, updatedState, ct);

        return new AdaptiveResponse(
            UpdatedMastery: updatedState.GetKnowledgeMastery(interaction.NodeId),
            Feedback: adaptiveContent.Feedback,
            NextContent: adaptiveContent.NextContent,
            ShouldAdvance: adaptiveContent.ShouldAdvance,
            RecommendedAction: adaptiveContent.RecommendedAction);
    }

    private async Task<AdaptiveContentResult> GenerateAdaptiveContentAsync(
        StudentInteraction interaction,
        StudentStateModel studentState,
        CancellationToken ct)
    {
        var prompt = EducationalPromptTemplates.AdaptiveFeedback
            .Replace("{response}", interaction.Response)
            .Replace("{expected}", interaction.ExpectedAnswer ?? "")
            .Replace("{responseTime}", interaction.ResponseTimeSeconds.ToString())
            .Replace("{attempts}", interaction.AttemptNumber.ToString())
            .Replace("{confidence}", $"{interaction.Confidence:P}");

        var feedback = await _gemini.GenerateTextAsync(prompt, ct);

        // Determine if student should advance
        var mastery = studentState.GetKnowledgeMastery(interaction.NodeId);
        var shouldAdvance = mastery.Level >= MasteryLevel.Developing && 
                           interaction.CorrectnesScore >= 0.7;

        string? nextContent = null;
        string recommendedAction;

        if (shouldAdvance)
        {
            recommendedAction = "advance";
        }
        else if (interaction.CorrectnesScore < 0.3)
        {
            recommendedAction = "remediate";
            // Generate simpler explanation
            var remediationRequest = new ContentGenerationRequest(
                ObjectiveId: Guid.Empty,
                Objective: $"Simplified explanation for struggling with {interaction.ContentType}",
                ContentType: "remediation",
                TargetLevel: BloomsLevel.Remember);
            
            var remediation = await _contentGenerator.GenerateWithToTAsync(
                remediationRequest, studentState, ct);
            nextContent = remediation.Content;
        }
        else
        {
            recommendedAction = "practice";
        }

        return new AdaptiveContentResult(
            Feedback: feedback,
            NextContent: nextContent,
            ShouldAdvance: shouldAdvance,
            RecommendedAction: recommendedAction);
    }

    /// <summary>
    /// Completes a module and records progress.
    /// </summary>
    public async Task<ModuleCompletionResult> CompleteModuleAsync(
        LearningSession session,
        Guid moduleId,
        CancellationToken ct = default)
    {
        var studentState = await _studentStateService.GetOrCreateStudentStateAsync(session.StudentId, ct);
        var module = session.LearningPath.Modules.FirstOrDefault(m => m.ModuleId == moduleId);

        if (module == null)
        {
            throw new InvalidOperationException($"Module {moduleId} not found");
        }

        var mastery = studentState.GetKnowledgeMastery(module.DomainNodeId);

        // Record session engagement
        await _studentStateService.RecordSessionAsync(
            session.StudentId,
            TimeSpan.FromMinutes(module.EstimatedMinutes),
            mastery.Confidence,
            ct);

        var passed = mastery.Level >= MasteryLevel.Developing;

        return new ModuleCompletionResult(
            ModuleId: moduleId,
            Passed: passed,
            MasteryLevel: mastery.Level,
            Confidence: mastery.Confidence,
            TimeSpent: TimeSpan.FromMinutes(module.EstimatedMinutes),
            RecommendedNextModule: passed 
                ? session.LearningPath.GetNextModule()?.ModuleId 
                : null);
    }
}

#region Supporting Types

/// <summary>
/// Learning goals for a session.
/// </summary>
public sealed record LearningGoals(
    int MaxModules = 5,
    int MaxMinutes = 60,
    IReadOnlyList<Guid>? TargetObjectives = null,
    BloomsLevel? MinimumBloomsLevel = null);

/// <summary>
/// Learning session.
/// </summary>
public sealed record LearningSession(
    Guid SessionId,
    Guid StudentId,
    LearningPath LearningPath,
    DateTimeOffset StartTime,
    SessionStatus Status);

/// <summary>
/// Session status.
/// </summary>
public enum SessionStatus
{
    Active,
    Paused,
    Completed,
    Abandoned
}

/// <summary>
/// Learning path with modules.
/// </summary>
public sealed record LearningPath(
    Guid PathId,
    IReadOnlyList<LearningModule> Modules,
    int CurrentModuleIndex,
    DateTimeOffset CreatedAt)
{
    public LearningModule? GetCurrentModule() => 
        CurrentModuleIndex < Modules.Count ? Modules[CurrentModuleIndex] : null;
    
    public LearningModule? GetNextModule() => 
        CurrentModuleIndex + 1 < Modules.Count ? Modules[CurrentModuleIndex + 1] : null;
}

/// <summary>
/// Learning module.
/// </summary>
public sealed record LearningModule(
    Guid ModuleId,
    Guid DomainNodeId,
    string Title,
    string Description,
    IReadOnlyList<LearningObjective> Objectives,
    IReadOnlyList<ContentNode> Content,
    int EstimatedMinutes,
    double Difficulty,
    ModuleStatus Status);

/// <summary>
/// Module status.
/// </summary>
public enum ModuleStatus
{
    NotStarted,
    InProgress,
    Completed,
    Skipped
}

/// <summary>
/// Personalized module content.
/// </summary>
public sealed record PersonalizedModuleContent(
    Guid ModuleId,
    string Title,
    IReadOnlyList<PersonalizedContent> PersonalizedContent,
    IReadOnlyList<ContentNode> AssessmentQuestions,
    int EstimatedMinutes,
    KnowledgeMastery StudentMastery);

/// <summary>
/// Student interaction with content.
/// </summary>
public sealed record StudentInteraction(
    Guid NodeId,
    string Response,
    string? ExpectedAnswer,
    double CorrectnesScore,
    double Confidence,
    int ResponseTimeSeconds,
    double Difficulty,
    BloomsLevel BloomsLevel,
    string ContentType,
    int AttemptNumber);

/// <summary>
/// Adaptive response to interaction.
/// </summary>
public sealed record AdaptiveResponse(
    KnowledgeMastery UpdatedMastery,
    string Feedback,
    string? NextContent,
    bool ShouldAdvance,
    string RecommendedAction);

/// <summary>
/// Result of adaptive content generation.
/// </summary>
internal sealed record AdaptiveContentResult(
    string Feedback,
    string? NextContent,
    bool ShouldAdvance,
    string RecommendedAction);

/// <summary>
/// Module completion result.
/// </summary>
public sealed record ModuleCompletionResult(
    Guid ModuleId,
    bool Passed,
    MasteryLevel MasteryLevel,
    double Confidence,
    TimeSpan TimeSpent,
    Guid? RecommendedNextModule);

#endregion
