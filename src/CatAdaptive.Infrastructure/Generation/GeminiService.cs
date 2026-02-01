using System.Text.Json;
using CatAdaptive.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.Infrastructure.Generation;

/// <summary>
/// Gemini AI service implementation for personalized learning.
/// </summary>
public sealed class GeminiService : IGeminiService
{
    private readonly string? _apiKey;
    private readonly string _modelName;
    private readonly ILogger<GeminiService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GeminiService(string? apiKey, string modelName, ILogger<GeminiService> logger)
    {
        _apiKey = apiKey;
        _modelName = modelName;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task<T> GenerateFromPromptAsync<T>(string prompt, CancellationToken ct = default) where T : class
    {
        var response = await GenerateTextAsync(prompt, ct);
        
        try
        {
            // Try to parse JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
                
                if (result != null)
                    return result;
            }
            
            // Try array format
            jsonStart = response.IndexOf('[');
            jsonEnd = response.LastIndexOf(']');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
                
                if (result != null)
                    return result;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON response from Gemini");
        }

        throw new InvalidOperationException($"Could not deserialize Gemini response to {typeof(T).Name}");
    }

    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Gemini API key not configured, using fallback response");
            return GenerateFallbackResponse(prompt);
        }

        try
        {
            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 4096
                }
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(requestUrl, content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var responseDoc = JsonDocument.Parse(responseJson);

            var text = responseDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed");
            return GenerateFallbackResponse(prompt);
        }
    }

    public async Task<string> ExecuteReActAsync(string prompt, CancellationToken ct = default)
    {
        var reactPrompt = $@"You are an AI assistant using the ReAct framework. For each step:
1. THOUGHT: Analyze what you need to do
2. ACTION: Take an action
3. OBSERVATION: Note the result

{prompt}

Execute this reasoning chain step by step.";

        return await GenerateTextAsync(reactPrompt, ct);
    }

    public async Task<IReadOnlyList<string>> GenerateThoughtPathsAsync(string prompt, int pathCount, CancellationToken ct = default)
    {
        var paths = new List<string>();

        for (int i = 0; i < pathCount; i++)
        {
            var pathPrompt = $@"{prompt}

Generate approach #{i + 1} of {pathCount}. Be creative and consider different angles.
Focus on a unique perspective for this approach.";

            var path = await GenerateTextAsync(pathPrompt, ct);
            paths.Add(path);
        }

        return paths;
    }

    public async Task<string> SelectBestPathAsync(string evaluationPrompt, CancellationToken ct = default)
    {
        return await GenerateTextAsync(evaluationPrompt, ct);
    }

    private string GenerateFallbackResponse(string prompt)
    {
        // Generate a simple fallback response when API is not available
        if (prompt.Contains("explanation", StringComparison.OrdinalIgnoreCase))
        {
            return "This is a generated explanation. The concept involves understanding the key principles and applying them in practice. Key points include foundational knowledge, practical application, and continuous learning.";
        }
        
        if (prompt.Contains("question", StringComparison.OrdinalIgnoreCase))
        {
            return "What are the main principles involved in this concept, and how would you apply them in a clinical setting?";
        }
        
        if (prompt.Contains("feedback", StringComparison.OrdinalIgnoreCase))
        {
            return "Great effort! You've shown understanding of the core concepts. Consider reviewing the practical applications to strengthen your knowledge further.";
        }
        
        if (prompt.Contains("mnemonic", StringComparison.OrdinalIgnoreCase))
        {
            return "Remember the key points using: LEARN - Listen, Engage, Apply, Review, Navigate.";
        }

        return "Generated content based on the provided context. This content is tailored to support your learning objectives and build upon your existing knowledge.";
    }
}
