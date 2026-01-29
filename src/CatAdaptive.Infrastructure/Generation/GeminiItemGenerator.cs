using System.Text;
using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using Google.GenAI;

namespace CatAdaptive.Infrastructure.Generation;

public sealed class GeminiItemGenerator : IItemGenerator
{
    private readonly Client _client;
    private readonly string _modelName;

    public GeminiItemGenerator(string? apiKey = null, string modelName = "gemini-2.0-flash-exp")
    {
        _client = string.IsNullOrWhiteSpace(apiKey) 
            ? new Client() 
            : new Client(apiKey: apiKey);
        _modelName = modelName;
    }

    public async Task<IReadOnlyList<ItemTemplate>> GenerateItemsAsync(
        IEnumerable<KnowledgeUnit> knowledgeUnits,
        CancellationToken ct = default)
    {
        var items = new List<ItemTemplate>();
        var unitsList = knowledgeUnits.ToList();
        var logPath = Path.Combine(Path.GetTempPath(), "gemini-debug.log");
        
        await File.AppendAllTextAsync(logPath, $"\n\n=== NEW RUN {DateTime.Now} ===\n");
        await File.AppendAllTextAsync(logPath, $"[GeminiItemGenerator] Starting generation for {unitsList.Count} units.\n");
        Console.WriteLine($"[GeminiItemGenerator] Starting generation for {unitsList.Count} units.");
        Console.WriteLine($"[GeminiItemGenerator] Debug log: {logPath}");

        foreach (var unit in unitsList)
        {
            if (unit.KeyPoints.Count == 0) 
            {
                var msg = $"[GeminiItemGenerator] Unit '{unit.Topic}' has 0 key points. Skipping.\n";
                await File.AppendAllTextAsync(logPath, msg);
                Console.WriteLine(msg.TrimEnd());
                continue;
            }

            try
            {
                var msg = $"[GeminiItemGenerator] Generating for unit: {unit.Topic} ({unit.KeyPoints.Count} points)\n";
                await File.AppendAllTextAsync(logPath, msg);
                Console.WriteLine(msg.TrimEnd());
                
                var generatedItems = await GenerateItemsForUnitAsync(unit, ct, logPath);
                
                var resultMsg = $"[GeminiItemGenerator] Generated {generatedItems.Count} items for unit: {unit.Topic}\n";
                await File.AppendAllTextAsync(logPath, resultMsg);
                Console.WriteLine(resultMsg.TrimEnd());
                
                items.AddRange(generatedItems);
            }
            catch (Exception ex)
            {
                var errMsg = $"[GeminiItemGenerator] Error generating items for unit {unit.Id}: {ex.Message}\n{ex.StackTrace}\n";
                await File.AppendAllTextAsync(logPath, errMsg);
                Console.WriteLine($"[GeminiItemGenerator] Error generating items for unit {unit.Id}: {ex.Message}");
            }
        }

        var finalMsg = $"[GeminiItemGenerator] Total items generated: {items.Count}\n";
        await File.AppendAllTextAsync(logPath, finalMsg);
        Console.WriteLine(finalMsg.TrimEnd());
        return items;
    }

    private async Task<List<ItemTemplate>> GenerateItemsForUnitAsync(KnowledgeUnit unit, CancellationToken ct, string logPath)
    {
        var prompt = BuildPrompt(unit);
        
        try 
        {
            await File.AppendAllTextAsync(logPath, $"[GeminiItemGenerator] Calling Gemini API for {unit.Topic}...\n");
            
            var response = await _client.Models.GenerateContentAsync(
                model: _modelName,
                contents: prompt);

            var responseText = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                var msg = $"[GeminiItemGenerator] Empty response from Gemini for unit: {unit.Topic}\n";
                await File.AppendAllTextAsync(logPath, msg);
                Console.WriteLine(msg.TrimEnd());
                return new List<ItemTemplate>();
            }

            await File.AppendAllTextAsync(logPath, $"[GeminiItemGenerator] Received response for {unit.Topic}: {responseText.Length} chars\n");
            await File.AppendAllTextAsync(logPath, $"Response preview: {responseText.Take(500)}...\n");
            Console.WriteLine($"[GeminiItemGenerator] Received response for {unit.Topic}: {responseText.Length} chars");
            
            var parsed = ParseGeminiResponse(responseText, unit);
            if (parsed.Count == 0)
            {
                var msg = $"[GeminiItemGenerator] Failed to parse any items from response for: {unit.Topic}\n";
                await File.AppendAllTextAsync(logPath, msg);
                await File.AppendAllTextAsync(logPath, $"Full response:\n{responseText}\n");
                Console.WriteLine(msg.TrimEnd());
            }
            return parsed;
        }
        catch (Exception ex)
        {
            var errMsg = $"[GeminiItemGenerator] Gemini API error for {unit.Topic}: {ex.Message}\n{ex.StackTrace}\n";
            await File.AppendAllTextAsync(logPath, errMsg);
            Console.WriteLine($"[GeminiItemGenerator] Gemini API error for {unit.Topic}: {ex.Message}");
            throw;
        }
    }

    private static string BuildPrompt(KnowledgeUnit unit)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("ACADEMIC QUESTION GENERATION SYSTEM - LEARNING OBJECTIVE ALIGNED");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("You are an expert pharmacology professor and educational assessment specialist.");
        sb.AppendLine("Your mission: Generate pedagogically sound, learning objective-aligned questions");
        sb.AppendLine("that progressively build student mastery using Bloom's Taxonomy.");
        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("📚 LEARNING OBJECTIVES (Map questions to these):");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        if (unit.LearningObjectives.Count > 0)
        {
            foreach (var objective in unit.LearningObjectives)
            {
                sb.AppendLine($"  ✓ {objective}");
            }
        }
        else
        {
            sb.AppendLine("  [No explicit objectives provided - infer from content]");
        }
        
        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📖 TOPIC: {unit.Topic}");
        if (!string.IsNullOrWhiteSpace(unit.Subtopic))
        {
            sb.AppendLine($"📌 SUBTOPIC: {unit.Subtopic}");
        }
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        sb.AppendLine("📝 KEY CONTENT:");
        foreach (var point in unit.KeyPoints)
        {
            sb.AppendLine($"  • {point}");
        }
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("🎯 BLOOM'S TAXONOMY FRAMEWORK (Use this progression):");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("Level 1: REMEMBER (Difficulty: -2.0 to -1.5)");
        sb.AppendLine("  → Recall facts, terms, definitions, mechanisms");
        sb.AppendLine("  → Verbs: Define, List, Name, Identify, State, Describe");
        sb.AppendLine("  → Example: 'What is the mechanism of action of metformin?'");
        sb.AppendLine();
        sb.AppendLine("Level 2: UNDERSTAND (Difficulty: -1.4 to -0.5)");
        sb.AppendLine("  → Explain concepts, classify, summarize, compare");
        sb.AppendLine("  → Verbs: Explain, Classify, Summarize, Compare, Interpret");
        sb.AppendLine("  → Example: 'Which statement correctly explains insulin resistance?'");
        sb.AppendLine();
        sb.AppendLine("Level 3: APPLY (Difficulty: -0.4 to 0.8)");
        sb.AppendLine("  → Use knowledge in clinical scenarios, calculate doses, select therapies");
        sb.AppendLine("  → Verbs: Apply, Calculate, Solve, Demonstrate, Use");
        sb.AppendLine("  → Example: 'A patient with CrCl 30 mL/min needs antibiotic dosing. Which adjustment?'");
        sb.AppendLine();
        sb.AppendLine("Level 4: ANALYZE (Difficulty: 0.9 to 1.5)");
        sb.AppendLine("  → Differentiate between options, analyze drug interactions, troubleshoot");
        sb.AppendLine("  → Verbs: Analyze, Differentiate, Distinguish, Examine, Compare");
        sb.AppendLine("  → Example: 'Compare efficacy and safety profiles of two antihypertensives for this patient.'");
        sb.AppendLine();
        sb.AppendLine("Level 5: EVALUATE (Difficulty: 1.6 to 2.0)");
        sb.AppendLine("  → Critique treatment plans, justify decisions, assess outcomes");
        sb.AppendLine("  → Verbs: Evaluate, Justify, Critique, Assess, Recommend");
        sb.AppendLine("  → Example: 'Evaluate this polypharmacy regimen and recommend optimization.'");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("⚙️ GENERATION REQUIREMENTS:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("1. GENERATE 2-3 QUESTIONS with PROGRESSIVE DIFFICULTY:");
        sb.AppendLine("   - Start with lower Bloom's level (Remember/Understand)");
        sb.AppendLine("   - Progress to higher levels (Apply/Analyze)");
        sb.AppendLine("   - Each question must map to a specific learning objective");
        sb.AppendLine();
        sb.AppendLine("2. QUESTION QUALITY STANDARDS:");
        sb.AppendLine("   ✓ Clinical relevance: Real-world pharmacy practice scenarios");
        sb.AppendLine("   ✓ Specificity: Avoid generic stems like 'Which is true about...'");
        sb.AppendLine("   ✓ Clarity: Unambiguous, single correct answer");
        sb.AppendLine("   ✓ Plausible distractors: Wrong but believable to novices");
        sb.AppendLine();
        sb.AppendLine("3. CONTENT FILTERING:");
        sb.AppendLine("   ✗ IGNORE: Contact info, phone numbers, acknowledgments, course logistics");
        sb.AppendLine("   ✓ FOCUS: Drug mechanisms, patient cases, dosing, interactions, monitoring");
        sb.AppendLine();
        sb.AppendLine("4. DISTRACTOR DESIGN:");
        sb.AppendLine("   - Use common misconceptions or related but incorrect concepts");
        sb.AppendLine("   - Avoid obvious wrong answers or generic placeholders");
        sb.AppendLine("   - Each distractor should be defensible to a novice learner");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("📋 OUTPUT FORMAT (Valid JSON Array):");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine(@"[
  {
    ""stem"": ""[Clear, specific question with clinical context if appropriate]"",
    ""choices"": [
      {""text"": ""[Correct answer with specific details]"", ""isCorrect"": true},
      {""text"": ""[Plausible distractor - common misconception]"", ""isCorrect"": false},
      {""text"": ""[Plausible distractor - related but incorrect]"", ""isCorrect"": false},
      {""text"": ""[Plausible distractor - opposite/extreme]"", ""isCorrect"": false}
    ],
    ""explanation"": ""[Why correct answer is right, with clinical reasoning]"",
    ""difficulty"": -1.5,
    ""bloomLevel"": ""Remember"",
    ""learningObjective"": ""[Which specific objective this tests]""
  },
  {
    ""stem"": ""[Next question, higher Bloom's level]"",
    ""choices"": [...],
    ""explanation"": ""..."",
    ""difficulty"": 0.5,
    ""bloomLevel"": ""Apply"",
    ""learningObjective"": ""[Relevant objective]""
  }
]");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("🚀 BEGIN GENERATION NOW");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    private static List<ItemTemplate> ParseGeminiResponse(string responseText, KnowledgeUnit unit)
    {
        var items = new List<ItemTemplate>();

        try
        {
            // Clean up response text if it contains markdown code blocks
            var jsonText = responseText.Trim();
            if (jsonText.StartsWith("```json"))
            {
                jsonText = jsonText.Substring(7);
                if (jsonText.EndsWith("```"))
                {
                    jsonText = jsonText.Substring(0, jsonText.Length - 3);
                }
            }
            else if (jsonText.StartsWith("```"))
            {
                jsonText = jsonText.Substring(3);
                if (jsonText.EndsWith("```"))
                {
                    jsonText = jsonText.Substring(0, jsonText.Length - 3);
                }
            }

            var jsonStart = jsonText.IndexOf('[');
            var jsonEnd = jsonText.LastIndexOf(']');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                jsonText = jsonText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var geminiItems = JsonSerializer.Deserialize<List<GeminiQuestionItem>>(jsonText, options);

                if (geminiItems != null)
                {
                    foreach (var geminiItem in geminiItems)
                    {
                        if (string.IsNullOrWhiteSpace(geminiItem.Stem) || geminiItem.Choices == null || geminiItem.Choices.Count < 2)
                        {
                            Console.WriteLine($"[GeminiItemGenerator] Skipping invalid item for {unit.Topic}. Stem: {geminiItem.Stem?.Length ?? 0}, Choices: {geminiItem.Choices?.Count ?? 0}");
                            continue;
                        }

                        var choices = geminiItem.Choices
                            .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                            .Select(c => ItemChoice.Create(c.Text!, c.IsCorrect))
                            .ToList();

                        if (choices.Count < 2) continue;

                        var difficulty = geminiItem.Difficulty ?? 0.0;
                        var discrimination = 1.0 + (Math.Abs(difficulty) * 0.3);
                        var parameter = new ItemParameter(difficulty, discrimination, 0.2);

                        var item = ItemTemplate.Create(
                            stem: geminiItem.Stem,
                            choices: choices,
                            format: ItemFormat.MultipleChoice,
                            parameter: parameter,
                            knowledgeUnitIds: new[] { unit.Id },
                            topic: unit.Topic,
                            subtopic: unit.Subtopic,
                            explanation: geminiItem.Explanation ?? string.Empty,
                            bloomLevel: geminiItem.BloomLevel ?? "Apply",
                            learningObjective: geminiItem.LearningObjective ?? string.Empty);

                        items.Add(item);
                    }
                }
            }
            else
            {
                Console.WriteLine($"[GeminiItemGenerator] Could not find JSON array brackets in response: {jsonText.Take(100)}...");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[GeminiItemGenerator] Failed to parse Gemini response as JSON: {ex.Message}");
            Console.WriteLine($"[GeminiItemGenerator] Problematic JSON: {responseText}");
        }

        return items;
    }

    private sealed class GeminiQuestionItem
    {
        public string? Stem { get; set; }
        public List<GeminiChoice>? Choices { get; set; }
        public string? Explanation { get; set; }
        public double? Difficulty { get; set; }
        public string? BloomLevel { get; set; }
        public string? LearningObjective { get; set; }
    }

    private sealed class GeminiChoice
    {
        public string? Text { get; set; }
        public bool IsCorrect { get; set; }
    }
}
