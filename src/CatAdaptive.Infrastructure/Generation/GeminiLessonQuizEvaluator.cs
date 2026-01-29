using System.Text;
using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Google.GenAI;

namespace CatAdaptive.Infrastructure.Generation;

public sealed class GeminiLessonQuizEvaluator : ILessonQuizEvaluator
{
    private readonly Client _client;
    private readonly string _modelName;

    public GeminiLessonQuizEvaluator(string? apiKey = null, string modelName = "gemini-2.0-flash-exp")
    {
        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new Client()
            : new Client(apiKey: apiKey);
        _modelName = modelName;
    }

    public async Task<LessonQuizResult> EvaluateAsync(
        LessonQuiz quiz,
        IReadOnlyList<LessonQuizAnswer> answers,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(quiz, answers);
        var response = await _client.Models.GenerateContentAsync(
            model: _modelName,
            contents: prompt);

        var responseText = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
        var results = ParseResults(responseText, quiz.Questions);

        var scorePercent = results.Count == 0
            ? 0
            : results.Average(r => r.Score) * 100;

        return new LessonQuizResult(
            DateTimeOffset.UtcNow,
            Math.Round(scorePercent, 2),
            results);
    }

    private static string BuildPrompt(LessonQuiz quiz, IReadOnlyList<LessonQuizAnswer> answers)
    {
        var answerLookup = answers.ToDictionary(a => a.QuestionId, a => a.ResponseText);
        var sb = new StringBuilder();
        sb.AppendLine("You are grading a learner quiz using the provided rubric.");
        sb.AppendLine("Return ONLY a JSON array of results:");
        sb.AppendLine("[");
        sb.AppendLine("  { \"questionId\": \"guid\", \"score\": 0.0, \"isCorrect\": true, \"feedback\": \"brief feedback\" }");
        sb.AppendLine("]");
        sb.AppendLine();
        sb.AppendLine("Score must be between 0 and 1.");
        sb.AppendLine();
        sb.AppendLine("QUESTIONS:");

        foreach (var question in quiz.Questions)
        {
            var responseText = answerLookup.TryGetValue(question.Id, out var response)
                ? response
                : string.Empty;

            sb.AppendLine("---");
            sb.AppendLine($"questionId: {question.Id}");
            sb.AppendLine($"type: {question.Type}");
            sb.AppendLine($"prompt: {question.Prompt}");
            sb.AppendLine($"expectedAnswer: {question.ExpectedAnswer}");
            sb.AppendLine($"learnerResponse: {responseText}");
            sb.AppendLine("rubric:");
            sb.AppendLine($"  requiredPoints: {string.Join("; ", question.Rubric.RequiredPoints)}");
            sb.AppendLine($"  keyConcepts: {string.Join("; ", question.Rubric.KeyConcepts)}");
            sb.AppendLine($"  commonMisconceptions: {string.Join("; ", question.Rubric.CommonMisconceptions)}");
            sb.AppendLine($"  minExplanationQuality: {question.Rubric.MinExplanationQuality}");
        }

        return sb.ToString();
    }

    private static IReadOnlyList<LessonQuizQuestionResult> ParseResults(
        string? responseText,
        IReadOnlyList<LessonQuizQuestion> questions)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return BuildFallbackResults(questions);
        }

        var jsonText = ExtractJson(responseText);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return BuildFallbackResults(questions);
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var parsed = JsonSerializer.Deserialize<List<QuizResultDto>>(jsonText, options) ?? new List<QuizResultDto>();
            var resultLookup = parsed.ToDictionary(r => r.QuestionId, r => r);
            var results = new List<LessonQuizQuestionResult>();

            foreach (var question in questions)
            {
                if (!resultLookup.TryGetValue(question.Id, out var dto))
                {
                    results.Add(new LessonQuizQuestionResult(question.Id, 0, false, "No evaluation returned."));
                    continue;
                }

                var score = Math.Clamp(dto.Score, 0, 1);
                results.Add(new LessonQuizQuestionResult(question.Id, score, dto.IsCorrect, dto.Feedback));
            }

            return results;
        }
        catch (JsonException)
        {
            return BuildFallbackResults(questions);
        }
    }

    private static string ExtractJson(string responseText)
    {
        var trimmed = responseText.Trim();
        if (trimmed.StartsWith("```"))
        {
            var fenceIndex = trimmed.IndexOf('\n');
            if (fenceIndex >= 0)
            {
                trimmed = trimmed[(fenceIndex + 1)..];
            }
            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed[..^3];
            }
        }

        var start = trimmed.IndexOf('[');
        var end = trimmed.LastIndexOf(']');
        return start >= 0 && end > start
            ? trimmed.Substring(start, end - start + 1)
            : string.Empty;
    }

    private static IReadOnlyList<LessonQuizQuestionResult> BuildFallbackResults(IReadOnlyList<LessonQuizQuestion> questions)
    {
        return questions
            .Select(q => new LessonQuizQuestionResult(q.Id, 0, false, "No evaluation available."))
            .ToList();
    }

    private sealed record QuizResultDto(
        Guid QuestionId,
        double Score,
        bool IsCorrect,
        string? Feedback);
}
