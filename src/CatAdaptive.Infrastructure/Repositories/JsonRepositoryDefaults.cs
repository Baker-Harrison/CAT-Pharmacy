using System.Text.Json;

namespace CatAdaptive.Infrastructure.Repositories;

internal static class JsonRepositoryDefaults
{
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions DefaultCaseInsensitive = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void EnsureDataFile(string dataDirectory, string filePath)
    {
        Directory.CreateDirectory(dataDirectory);
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "[]");
        }
    }

    public static void EnsureDirectoryForFile(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
