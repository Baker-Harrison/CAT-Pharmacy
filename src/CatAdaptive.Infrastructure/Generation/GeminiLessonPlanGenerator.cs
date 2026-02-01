using System.Text;
using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;
using Google.GenAI;

namespace CatAdaptive.Infrastructure.Generation;

public sealed class GeminiLessonPlanGenerator : ILessonPlanGenerator
{
    private readonly Client _client;
    private readonly string _modelName;

    public GeminiLessonPlanGenerator(string? apiKey = null, string modelName = "gemini-2.0-flash-exp")
    {
        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new Client()
            : new Client(apiKey: apiKey);
        _modelName = modelName;
    }

    public Task<IReadOnlyList<LessonPlan>> GenerateInitialLessonsAsync(
        ContentGraph contentGraph,
        CancellationToken ct = default)
    {
        var prompt = BuildInitialPrompt(contentGraph);
        return GenerateLessonsAsync(prompt, contentGraph, isRemediation: false, focusConceptId: null, ct);
    }

    public Task<IReadOnlyList<LessonPlan>> GenerateNextLessonsAsync(
        ContentGraph contentGraph,
        KnowledgeGraph knowledgeGraph,
        IReadOnlyList<Guid> existingConceptIds,
        CancellationToken ct = default)
    {
        var prompt = BuildNextPrompt(contentGraph, knowledgeGraph, existingConceptIds);
        return GenerateLessonsAsync(prompt, contentGraph, isRemediation: false, focusConceptId: null, ct);
    }

    public async Task<LessonPlan?> GenerateRemediationLessonAsync(
        ContentGraph contentGraph,
        KnowledgeGraph knowledgeGraph,
        Guid conceptId,
        CancellationToken ct = default)
    {
        var prompt = BuildRemediationPrompt(contentGraph, knowledgeGraph, conceptId);
        var lessons = await GenerateLessonsAsync(prompt, contentGraph, isRemediation: true, focusConceptId: conceptId, ct);
        return lessons.FirstOrDefault();
    }

    private async Task<IReadOnlyList<LessonPlan>> GenerateLessonsAsync(
        string prompt,
        ContentGraph contentGraph,
        bool isRemediation,
        Guid? focusConceptId,
        CancellationToken ct)
    {
        Console.WriteLine($"DEBUG: Generating Lessons. Prompt Length: {prompt.Length} chars.");
        Console.WriteLine("DEBUG: Sending prompt to Gemini...");
        
        var response = await _client.Models.GenerateContentAsync(
            model: _modelName,
            contents: prompt);

        var responseText = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            Console.WriteLine("DEBUG: Received empty response from Gemini.");
            return Array.Empty<LessonPlan>();
        }

        Console.WriteLine($"DEBUG: Raw Response Length: {responseText.Length}");

        var lessonDtos = ParseLessonPlans(responseText);
        Console.WriteLine($"DEBUG: Parsed {lessonDtos.Count} lesson DTOs.");

        var lessons = new List<LessonPlan>();

        foreach (var dto in lessonDtos)
        {
            var lesson = MapLessonPlan(dto, contentGraph, isRemediation, focusConceptId);
            if (lesson != null)
            {
                lessons.Add(lesson);
            }
            else
            {
                Console.WriteLine($"DEBUG: Failed to map lesson DTO for concept: {dto.ConceptId}");
            }
        }

        Console.WriteLine($"DEBUG: Successfully mapped {lessons.Count} lessons.");
        return lessons;
    }

    private LessonPlan? MapLessonPlan(
        LessonPlanDto dto,
        ContentGraph contentGraph,
        bool isRemediation,
        Guid? focusConceptId)
    {
        var conceptId = FindConceptId(dto.ConceptId, contentGraph, focusConceptId);
        if (conceptId == null)
        {
            Console.WriteLine($"DEBUG: Could not resolve Concept ID for '{dto.ConceptId}'.");
            return null;
        }

        var conceptNode = contentGraph.GetNode(conceptId.Value);
        if (conceptNode == null)
        {
            Console.WriteLine($"DEBUG: Concept node not found in graph for ID: {conceptId.Value}");
            return null;
        }

        var title = string.IsNullOrWhiteSpace(dto.Title) ? conceptNode.Text : dto.Title;
        var summary = dto.Summary ?? string.Empty;
        var readMinutes = Math.Clamp(dto.EstimatedReadMinutes ?? 18, 15, 20);
        var sections = dto.Sections
            ?.Select(MapSection)
            .Where(section => section != null)
            .Cast<LessonSection>()
            .ToList() ?? new List<LessonSection>();

        if (sections.Count == 0)
        {
            sections.Add(new LessonSection(
                "Overview",
                summary,
                Array.Empty<LessonPrompt>(),
                Guid.NewGuid()));
        }

        EnsureActiveRecallStructure(sections, title);

        var quiz = MapQuiz(dto.Quiz, conceptId.Value);
        if (quiz.Questions.Count == 0)
        {
            quiz = BuildFallbackQuiz(conceptId.Value, title);
        }

        return LessonPlan.Create(
            conceptId.Value,
            title,
            summary,
            readMinutes,
            isRemediation || dto.IsRemediation == true,
            sections,
            quiz);
    }

    private Guid? FindConceptId(string? conceptIdStr, ContentGraph contentGraph, Guid? fallback)
    {
        if (Guid.TryParse(conceptIdStr, out var parsed))
        {
            return parsed;
        }

        if (!string.IsNullOrWhiteSpace(conceptIdStr))
        {
            var match = contentGraph.GetConcepts()
                .FirstOrDefault(c => string.Equals(c.Text, conceptIdStr.Trim(), StringComparison.OrdinalIgnoreCase));
            
            if (match != null)
            {
                return match.Id;
            }
        }

        return fallback;
    }

    private static LessonSection? MapSection(LessonSectionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Heading) && string.IsNullOrWhiteSpace(dto.Body))
        {
            return null;
        }

        var prompts = dto.Prompts
            ?.Where(p => !string.IsNullOrWhiteSpace(p.Prompt))
            .Select(p => new LessonPrompt(p.Prompt!.Trim(), p.ExpectedAnswer?.Trim()))
            .ToList() ?? new List<LessonPrompt>();

        return new LessonSection(
            dto.Heading?.Trim() ?? "Lesson",
            dto.Body?.Trim() ?? string.Empty,
            prompts,
            Guid.NewGuid()); // Generate a unique ID for each section
    }

    private static void EnsureActiveRecallStructure(List<LessonSection> sections, string title)
    {
        var totalPrompts = sections.Sum(section => section.Prompts.Count);
        if (totalPrompts < 2)
        {
            sections.Add(new LessonSection(
                "Active Recall and Spaced Review",
                $"Use this section to retrieve key ideas from {title} without looking at the notes.",
                new List<LessonPrompt>
                {
                    new("From memory, list 3 key ideas from this lesson.", null),
                    new("Schedule a 24-hour recall: write a short summary and note any gaps.", null)
                },
                Guid.NewGuid()));
            return;
        }

        var hasSpacedPrompt = sections.Any(section => section.Prompts.Any(prompt =>
            prompt.Prompt.Contains("24-hour", StringComparison.OrdinalIgnoreCase) ||
            prompt.Prompt.Contains("spaced", StringComparison.OrdinalIgnoreCase)));

        if (!hasSpacedPrompt)
        {
            var last = sections[^1];
            var updatedPrompts = last.Prompts.ToList();
            updatedPrompts.Add(new LessonPrompt(
                "In 24 hours, recall the lesson without notes and write a 3 sentence summary.",
                null));
            sections[^1] = new LessonSection(last.Heading, last.Body, updatedPrompts, last.Id);
        }
    }

    private static LessonQuiz MapQuiz(LessonQuizDto? dto, Guid conceptId)
    {
        var questions = dto?.Questions?
            .Where(q => !string.IsNullOrWhiteSpace(q.Prompt))
            .Select(q => MapQuestion(q, conceptId))
            .Where(q => q != null)
            .Cast<LessonQuizQuestion>()
            .ToList() ?? new List<LessonQuizQuestion>();

        return new LessonQuiz(questions);
    }

    private static LessonQuizQuestion? MapQuestion(LessonQuizQuestionDto dto, Guid conceptId)
    {
        if (string.IsNullOrWhiteSpace(dto.Prompt))
        {
            return null;
        }

        var questionType = ParseQuestionType(dto.Type);
        var rubric = dto.Rubric?.ToEvaluationRubric() ?? EvaluationRubric.Create();
        var expected = dto.ExpectedAnswer?.Trim() ?? string.Empty;

        // NEW LOGIC: Parse or Generate ID
        Guid id;
        if (Guid.TryParse(dto.Id, out var parsedId))
        {
            id = parsedId;
        }
        else
        {
            id = Guid.NewGuid();
        }

        return new LessonQuizQuestion(
            id,
            dto.ConceptId ?? conceptId,
            questionType,
            dto.Prompt.Trim(),
            expected,
            rubric);
    }

    private static LessonQuiz BuildFallbackQuiz(Guid conceptId, string title)
    {
        var question = new LessonQuizQuestion(
            Guid.NewGuid(),
            conceptId,
            LessonQuizQuestionType.OpenResponse,
            $"Explain the core idea of {title} in your own words.",
            string.Empty,
            EvaluationRubric.Create());

        return new LessonQuiz(new List<LessonQuizQuestion> { question });
    }

    private static LessonQuizQuestionType ParseQuestionType(string? type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "fillinblank" => LessonQuizQuestionType.FillInBlank,
            "fill-in-blank" => LessonQuizQuestionType.FillInBlank,
            "fill_in_blank" => LessonQuizQuestionType.FillInBlank,
            _ => LessonQuizQuestionType.OpenResponse
        };
    }

    private IReadOnlyList<LessonPlanDto> ParseLessonPlans(string responseText)
    {
        var jsonText = ExtractJson(responseText);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            Console.WriteLine("DEBUG: ExtractJson returned empty string. Code block parsing might have failed.");
            return Array.Empty<LessonPlanDto>();
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            return JsonSerializer.Deserialize<List<LessonPlanDto>>(jsonText, options) ?? new List<LessonPlanDto>();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"DEBUG: JSON Deserialization failed: {ex.Message}");
            Console.WriteLine($"DEBUG: Path: {ex.Path} | LineNumber: {ex.LineNumber} | BytePositionInLine: {ex.BytePositionInLine}");
            return Array.Empty<LessonPlanDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Unexpected error during JSON parsing: {ex.Message}");
            return Array.Empty<LessonPlanDto>();
        }
    }

    private static string ExtractJson(string responseText)
    {
        var trimmed = responseText.Trim();
        
        // Remove markdown code fences if present
        if (trimmed.StartsWith("```"))
        {
            var fenceIndex = trimmed.IndexOf('\n');
            if (fenceIndex >= 0)
            {
                trimmed = trimmed[(fenceIndex + 1)..];
            }
            var lastFence = trimmed.LastIndexOf("```");
            if (lastFence >= 0)
            {
                trimmed = trimmed[..lastFence];
            }
            trimmed = trimmed.Trim();
        }

        // Find the first opening bracket
        var start = trimmed.IndexOf('[');
        if (start < 0) return string.Empty;

        // Balanced bracket counting to find the true end of the JSON array
        int balance = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < trimmed.Length; i++)
        {
            char c = trimmed[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '[') balance++;
                else if (c == ']')
                {
                    balance--;
                    if (balance == 0)
                    {
                        // Found matching closing bracket
                        return trimmed.Substring(start, i - start + 1);
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string BuildInitialPrompt(ContentGraph contentGraph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert instructor designing a first learning sequence.");
        sb.AppendLine("The learner has no knowledge graph history. Pick the simplest concepts by comprehension difficulty.");
        sb.AppendLine("Decide how many lessons are needed to build a foundation.");
        sb.AppendLine("Each lesson must be 15-20 minutes of reading and follow active learning principles.");
        sb.AppendLine("Prioritize active recall and spaced repetition in the structure:");
        sb.AppendLine("- Start with a short retrieval warm-up (2-3 prompts).");
        sb.AppendLine("- Embed recall prompts after each section.");
        sb.AppendLine("- End with a spaced review prompt that schedules a 24-hour recall.");
        sb.AppendLine("Finish each lesson with a longer quiz (prefer 10-14 questions). Use only FillInBlank or OpenResponse types.");
        sb.AppendLine("Return ONLY valid JSON array with this schema:");
        sb.AppendLine("[");
        sb.AppendLine("  {");
        sb.AppendLine("    \"conceptId\": \"guid\",");
        sb.AppendLine("    \"title\": \"lesson title\",");
        sb.AppendLine("    \"summary\": \"short summary\",");
        sb.AppendLine("    \"estimatedReadMinutes\": 18,");
        sb.AppendLine("    \"isRemediation\": false,");
        sb.AppendLine("    \"sections\": [");
        sb.AppendLine("      {");
        sb.AppendLine("        \"heading\": \"section heading\",");
        sb.AppendLine("        \"body\": \"paragraphs...\",");
        sb.AppendLine("        \"prompts\": [ { \"prompt\": \"reflection question\", \"expectedAnswer\": \"optional\" } ]");
        sb.AppendLine("      }");
        sb.AppendLine("    ],");
        sb.AppendLine("    \"quiz\": {");
        sb.AppendLine("      \"questions\": [");
        sb.AppendLine("        {");
        sb.AppendLine("          \"id\": \"guid\",");
        sb.AppendLine("          \"type\": \"FillInBlank|OpenResponse\",");
        sb.AppendLine("          \"prompt\": \"question text\",");
        sb.AppendLine("          \"expectedAnswer\": \"expected answer\",");
        sb.AppendLine("          \"rubric\": {");
        sb.AppendLine("            \"requiredPoints\": [\"\"],");
        sb.AppendLine("            \"keyConcepts\": [\"\"],");
        sb.AppendLine("            \"commonMisconceptions\": [\"\"],");
        sb.AppendLine("            \"minExplanationQuality\": 0.7");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      ]");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("]");
        sb.AppendLine();
        sb.AppendLine("CONCEPTS (choose simplest):");
        AppendConceptSummaries(sb, contentGraph);
        return sb.ToString();
    }

    private static string BuildNextPrompt(
        ContentGraph contentGraph,
        KnowledgeGraph knowledgeGraph,
        IReadOnlyList<Guid> existingConceptIds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are continuing a learning plan based on prior quiz results.");
        sb.AppendLine("Pick the next lessons based on knowledge graph mastery and content graph coverage.");
        sb.AppendLine("Avoid full lesson repeats unless a concept is listed in the priority review list.");
        sb.AppendLine("Each lesson must be 15-20 minutes of reading with active learning prompts.");
        sb.AppendLine("Prioritize active recall and spaced repetition:");
        sb.AppendLine("- Start with a short retrieval warm-up.");
        sb.AppendLine("- Include spaced review prompts for priority review concepts.");
        sb.AppendLine("Finish each lesson with a longer quiz (prefer 10-14 questions). Use only FillInBlank or OpenResponse.");
        sb.AppendLine("Return ONLY the JSON array matching the previous schema.");
        sb.AppendLine();
        sb.AppendLine("EXISTING LESSON CONCEPT IDS:");
        sb.AppendLine(string.Join(", ", existingConceptIds));
        sb.AppendLine();
        AppendPriorityReviewConcepts(sb, contentGraph, knowledgeGraph);
        sb.AppendLine();
        sb.AppendLine("MASTERY STATES:");
        foreach (var concept in contentGraph.GetConcepts())
        {
            var mastery = knowledgeGraph.GetMastery(concept.Id);
            sb.AppendLine($"- {concept.Id}: {mastery.State}, decayRisk={mastery.DecayRisk:F2}, correct={mastery.CorrectCount}, incorrect={mastery.IncorrectCount}");
        }
        sb.AppendLine();
        sb.AppendLine("CONCEPTS:");
        AppendConceptSummaries(sb, contentGraph);
        return sb.ToString();
    }

    private static string BuildRemediationPrompt(
        ContentGraph contentGraph,
        KnowledgeGraph knowledgeGraph,
        Guid conceptId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Create one remediation lesson for the specified concept.");
        sb.AppendLine("The learner scored below 80% on the quiz. Use active learning and guided practice.");
        sb.AppendLine("Prioritize active recall and spaced repetition prompts, including a 24-hour follow-up recall.");
        sb.AppendLine("Lesson length must be 15-20 minutes. Include a longer quiz at the end.");
        sb.AppendLine("Mark the lesson as remediation.");
        sb.AppendLine("Return ONLY a JSON array with a single lesson using the same schema.");
        sb.AppendLine();
        sb.AppendLine($"TARGET CONCEPT ID: {conceptId}");
        sb.AppendLine();
        sb.AppendLine("CURRENT MASTERY:");
        var mastery = knowledgeGraph.GetMastery(conceptId);
        sb.AppendLine($"- State={mastery.State}, decayRisk={mastery.DecayRisk:F2}, correct={mastery.CorrectCount}, incorrect={mastery.IncorrectCount}");
        sb.AppendLine();
        sb.AppendLine("CONCEPT DETAILS:");
        AppendConceptSummaries(sb, contentGraph, conceptId);
        return sb.ToString();
    }

    private static void AppendConceptSummaries(StringBuilder sb, ContentGraph contentGraph, Guid? focusConceptId = null)
    {
        var concepts = contentGraph.GetConcepts();
        if (focusConceptId.HasValue)
        {
            concepts = concepts.Where(c => c.Id == focusConceptId.Value).ToList();
        }

        foreach (var concept in concepts)
        {
            var explanations = contentGraph.GetExplanations(concept.Id)
                .Select(e => Truncate(e.Text, 180))
                .Take(3)
                .ToList();

            sb.AppendLine($"- {concept.Id} | {concept.Text}");
            if (explanations.Count > 0)
            {
                sb.AppendLine("  Explanations:");
                foreach (var explanation in explanations)
                {
                    sb.AppendLine($"    - {explanation}");
                }
            }
        }
    }

    private static void AppendPriorityReviewConcepts(
        StringBuilder sb,
        ContentGraph contentGraph,
        KnowledgeGraph knowledgeGraph)
    {
        var priorityConcepts = knowledgeGraph.GetAtRiskConcepts(0.6)
            .Concat(knowledgeGraph.GetConceptsByState(MasteryState.Fragile))
            .GroupBy(m => m.ConceptId)
            .Select(g => g.First())
            .OrderByDescending(m => m.DecayRisk)
            .ThenBy(m => m.State)
            .Take(6)
            .ToList();

        sb.AppendLine("PRIORITY REVIEW CONCEPTS (spaced repetition):");
        if (priorityConcepts.Count == 0)
        {
            sb.AppendLine("- None");
            return;
        }

        foreach (var mastery in priorityConcepts)
        {
            var concept = contentGraph.GetNode(mastery.ConceptId);
            var label = concept?.Text ?? mastery.ConceptId.ToString();
            sb.AppendLine($"- {mastery.ConceptId} | {label} | state={mastery.State}, decayRisk={mastery.DecayRisk:F2}");
        }
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

    private sealed record LessonPlanDto(
        string? ConceptId,
        string? Title,
        string? Summary,
        int? EstimatedReadMinutes,
        bool? IsRemediation,
        List<LessonSectionDto>? Sections,
        LessonQuizDto? Quiz);

    private sealed record LessonSectionDto(
        string? Heading,
        string? Body,
        List<LessonPromptDto>? Prompts);

    private sealed record LessonPromptDto(
        string? Prompt,
        string? ExpectedAnswer);

    private sealed record LessonQuizDto(List<LessonQuizQuestionDto>? Questions);

    private sealed record LessonQuizQuestionDto(
        string? Id, // CHANGED FROM Guid? TO string?
        Guid? ConceptId,
        string? Type,
        string? Prompt,
        string? ExpectedAnswer,
        LessonRubricDto? Rubric);

    private sealed record LessonRubricDto(
        List<string>? RequiredPoints,
        List<string>? KeyConcepts,
        List<string>? CommonMisconceptions,
        double? MinExplanationQuality)
    {
        public EvaluationRubric ToEvaluationRubric()
        {
            return EvaluationRubric.Create(
                RequiredPoints,
                KeyConcepts,
                CommonMisconceptions,
                MinExplanationQuality ?? 0.7);
        }
    }
}
