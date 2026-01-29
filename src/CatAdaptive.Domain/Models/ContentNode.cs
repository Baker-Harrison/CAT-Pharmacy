namespace CatAdaptive.Domain.Models;

/// <summary>
/// A node in the Content Graph representing instructional content.
/// Nodes are immutable and versioned for diffing.
/// </summary>
public sealed record ContentNode
{
    public Guid Id { get; init; }
    
    /// <summary>Type of content this node represents.</summary>
    public ContentNodeType Type { get; init; }
    
    /// <summary>The textual content of this node.</summary>
    public string Text { get; init; } = string.Empty;
    
    /// <summary>Whether content originated internally (slides) or externally (fetched).</summary>
    public ContentOrigin SourceOrigin { get; init; }
    
    /// <summary>Reference to source (slide ID for internal, citation for external).</summary>
    public string SourceRef { get; init; } = string.Empty;
    
    /// <summary>Confidence score 0-1 (higher = more reliable).</summary>
    public double Confidence { get; init; } = 1.0;
    
    /// <summary>Whether this content aligns with instructor materials.</summary>
    public bool InstructorAligned { get; init; } = true;
    
    /// <summary>Semantic version for diffing (content hash or UUID).</summary>
    public string Version { get; init; } = string.Empty;
    
    /// <summary>When this node was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    
    /// <summary>When this node was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Creates a new internal content node from course materials.
    /// </summary>
    public static ContentNode CreateInternal(
        ContentNodeType type,
        string text,
        string sourceSlideId,
        double confidence = 1.0)
    {
        var now = DateTimeOffset.UtcNow;
        var version = ComputeContentHash(text);
        
        return new ContentNode
        {
            Id = Guid.NewGuid(),
            Type = type,
            Text = text.Trim(),
            SourceOrigin = ContentOrigin.Internal,
            SourceRef = sourceSlideId,
            Confidence = Math.Clamp(confidence, 0.0, 1.0),
            InstructorAligned = true,
            Version = version,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Creates a new external content node from acquired sources.
    /// </summary>
    public static ContentNode CreateExternal(
        ContentNodeType type,
        string text,
        string citationRef,
        double confidence = 0.8)
    {
        var now = DateTimeOffset.UtcNow;
        var version = ComputeContentHash(text);
        
        return new ContentNode
        {
            Id = Guid.NewGuid(),
            Type = type,
            Text = text.Trim(),
            SourceOrigin = ContentOrigin.External,
            SourceRef = citationRef,
            Confidence = Math.Clamp(confidence, 0.0, 1.0),
            InstructorAligned = false,
            Version = version,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Creates an updated version of this node with new content.
    /// </summary>
    public ContentNode WithUpdatedContent(string newText, double? newConfidence = null)
    {
        return this with
        {
            Text = newText.Trim(),
            Version = ComputeContentHash(newText),
            Confidence = newConfidence.HasValue ? Math.Clamp(newConfidence.Value, 0.0, 1.0) : Confidence,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string ComputeContentHash(string text)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text.Trim().ToLowerInvariant());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash)[..16]; // First 16 chars for brevity
    }
}
