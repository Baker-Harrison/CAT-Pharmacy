namespace CatAdaptive.Domain.Models;

/// <summary>
/// Mastery states for concept knowledge (the learner's proficiency level).
/// States progress from Unknown to TransferReady based on evidence.
/// </summary>
public enum MasteryState
{
    /// <summary>No evidence of knowledge (never attempted or tested).</summary>
    Unknown = 0,
    
    /// <summary>Partial evidence; may have partial success but not consistent.</summary>
    Fragile = 1,
    
    /// <summary>Consistent correct performance (2+ correct with time/format separation).</summary>
    Functional = 2,
    
    /// <summary>Application + explain-why success; deep understanding demonstrated.</summary>
    Robust = 3,
    
    /// <summary>Novel integration success; can transfer knowledge to new contexts.</summary>
    TransferReady = 4
}

/// <summary>
/// Prompt formats for evidence collection.
/// </summary>
public enum PromptFormat
{
    /// <summary>Short answer response (default).</summary>
    ShortAnswer,
    
    /// <summary>Explain-why question requiring reasoning.</summary>
    ExplainWhy,
    
    /// <summary>Application/clinical scenario question.</summary>
    Application,
    
    /// <summary>Integration question combining multiple concepts (exam mode).</summary>
    Integration
}

/// <summary>
/// Error type classification for failed responses.
/// </summary>
public enum ErrorType
{
    /// <summary>No error (correct response).</summary>
    None,
    
    /// <summary>Factual error - incorrect information recalled.</summary>
    Factual,
    
    /// <summary>Conceptual error - misunderstanding of the concept.</summary>
    Conceptual,
    
    /// <summary>Procedural error - wrong steps or process.</summary>
    Procedural,
    
    /// <summary>Prerequisite gap - missing foundational knowledge.</summary>
    PrerequisiteGap,
    
    /// <summary>Careless error - likely knows but made a mistake.</summary>
    Careless,
    
    /// <summary>Incomplete response - partial understanding shown.</summary>
    Incomplete
}
