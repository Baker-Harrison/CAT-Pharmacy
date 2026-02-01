using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Application.Services;

/// <summary>
/// Tree of Thoughts content generator for personalized learning.
/// </summary>
public sealed class ToTContentGenerator
{
    private readonly IGeminiService _gemini;
    private readonly ILogger<ToTContentGenerator> _logger;

    public ToTContentGenerator(
        IGeminiService gemini,
        ILogger<ToTContentGenerator> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    /// <summary>
    /// Generates personalized content using Tree of Thoughts approach.
    /// </summary>
    public async Task<PersonalizedContent> GenerateWithToTAsync(
        ContentGenerationRequest request,
        StudentStateModel studentState,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating ToT content for objective: {Objective}", request.Objective);

        var totPrompt = FormatToTPrompt(request, studentState);

        try
        {
            // Generate multiple thought paths
            var thoughtPaths = await _gemini.GenerateThoughtPathsAsync(totPrompt, 4, ct);
            _logger.LogInformation("Generated {Count} thought paths", thoughtPaths.Count);

            // Evaluate and select best path
            var evaluationPrompt = CreateEvaluationPrompt(thoughtPaths, request, studentState);
            var bestPath = await _gemini.SelectBestPathAsync(evaluationPrompt, ct);

            // Generate final content from best path
            var generationPrompt = CreateFinalGenerationPrompt(bestPath, request, studentState);
            var content = await _gemini.GenerateTextAsync(generationPrompt, ct);

            return new PersonalizedContent(
                Id: Guid.NewGuid(),
                ObjectiveId: request.ObjectiveId,
                Content: content,
                ContentType: request.ContentType,
                PersonalizationRationale: bestPath,
                EstimatedTimeMinutes: EstimateTime(content),
                CreatedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ToT generation failed, using fallback");
            return await GenerateFallbackContentAsync(request, studentState, ct);
        }
    }

    private string FormatToTPrompt(ContentGenerationRequest request, StudentStateModel studentState)
    {
        var strengths = studentState.CurrentAnalysis.StrengthAreas.Take(3);
        var gaps = studentState.CurrentAnalysis.CriticalGaps.Take(3).Select(g => g.Description);

        return EducationalPromptTemplates.TreeOfThoughts
            .Replace("{objective}", request.Objective)
            .Replace("{masteryLevel}", $"{studentState.CurrentAnalysis.OverallMasteryScore:P}")
            .Replace("{strengths}", string.Join(", ", strengths))
            .Replace("{gaps}", string.Join(", ", gaps))
            .Replace("{modality}", studentState.Preferences.PreferredModality.ToString())
            .Replace("{confidence}", $"{studentState.Engagement.AverageConfidence:P}");
    }

    private string CreateEvaluationPrompt(
        IReadOnlyList<string> thoughtPaths,
        ContentGenerationRequest request,
        StudentStateModel studentState)
    {
        var pathsText = string.Join("\n\n", 
            thoughtPaths.Select((p, i) => $"PATH {i + 1}:\n{p}"));

        return $@"Evaluate these thought paths for educational effectiveness:

{pathsText}

EVALUATION CRITERIA:
1. Learning effectiveness (aligns with objective: {request.Objective})
2. Engagement potential (student prefers: {studentState.Preferences.PreferredModality})
3. Appropriateness for mastery level: {studentState.CurrentAnalysis.OverallMasteryScore:P}
4. Feasibility of implementation

Select the best path and provide justification.
Output format: 
SELECTED_PATH: [number]
JUSTIFICATION: [reasoning]
SYNTHESIZED_APPROACH: [combined best elements]";
    }

    private string CreateFinalGenerationPrompt(
        string bestPath,
        ContentGenerationRequest request,
        StudentStateModel studentState)
    {
        return $@"Generate final learning content based on this approach:

SELECTED APPROACH:
{bestPath}

REQUIREMENTS:
- Objective: {request.Objective}
- Content Type: {request.ContentType}
- Student Mastery: {studentState.CurrentAnalysis.OverallMasteryScore:P}
- Preferred Modality: {studentState.Preferences.PreferredModality}

Create a complete, personalized learning experience with:
1. Engaging hook (1-2 sentences)
2. Clear explanations with examples
3. Practice opportunity
4. Self-check question

Keep content focused and appropriate for the student's level.";
    }

    private async Task<PersonalizedContent> GenerateFallbackContentAsync(
        ContentGenerationRequest request,
        StudentStateModel studentState,
        CancellationToken ct)
    {
        var simplePrompt = $@"Generate educational content for: {request.Objective}

Student Level: {studentState.CurrentAnalysis.OverallMasteryScore:P}
Content Type: {request.ContentType}

Create a clear, structured explanation with:
1. Introduction
2. Key concepts
3. Example
4. Summary";

        var content = await _gemini.GenerateTextAsync(simplePrompt, ct);

        return new PersonalizedContent(
            Id: Guid.NewGuid(),
            ObjectiveId: request.ObjectiveId,
            Content: content,
            ContentType: request.ContentType,
            PersonalizationRationale: "Fallback generation used",
            EstimatedTimeMinutes: 10,
            CreatedAt: DateTimeOffset.UtcNow);
    }

    private int EstimateTime(string content)
    {
        // Rough estimate: 200 words per minute reading speed
        var wordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(5, wordCount / 200 + 2); // Minimum 5 minutes, add 2 for processing
    }
}

/// <summary>
/// Request for content generation.
/// </summary>
public sealed record ContentGenerationRequest(
    Guid ObjectiveId,
    string Objective,
    string ContentType,
    BloomsLevel TargetLevel,
    IReadOnlyList<string>? AdditionalContext = null);

/// <summary>
/// Personalized content generated for a student.
/// </summary>
public sealed record PersonalizedContent(
    Guid Id,
    Guid ObjectiveId,
    string Content,
    string ContentType,
    string PersonalizationRationale,
    int EstimatedTimeMinutes,
    DateTimeOffset CreatedAt);
