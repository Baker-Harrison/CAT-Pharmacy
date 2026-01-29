using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CatAdaptive.Domain.Models;

public sealed record KnowledgeUnit(
    Guid Id,
    string Topic,
    string Subtopic,
    string SourceSlideId,
    string Summary,
    IReadOnlyList<string> KeyPoints,
    IReadOnlyList<string> LearningObjectives)
{
    public static KnowledgeUnit Create(
        string topic,
        string subtopic,
        string sourceSlideId,
        string summary,
        IEnumerable<string> keyPoints,
        IEnumerable<string>? learningObjectives = null)
    {
        topic = string.IsNullOrWhiteSpace(topic) ? "General" : topic.Trim();
        subtopic = string.IsNullOrWhiteSpace(subtopic) ? string.Empty : subtopic.Trim();
        summary = string.IsNullOrWhiteSpace(summary) ? string.Empty : summary.Trim();

        var points = keyPoints?.Where(p => !string.IsNullOrWhiteSpace(p))
                       .Select(p => p.Trim())
                       .ToList() ?? new List<string>();

        var objectives = learningObjectives?.Where(o => !string.IsNullOrWhiteSpace(o))
                           .Select(o => o.Trim())
                           .ToList() ?? new List<string>();

        return new KnowledgeUnit(
            Guid.NewGuid(),
            topic,
            subtopic,
            sourceSlideId,
            summary,
            new ReadOnlyCollection<string>(points),
            new ReadOnlyCollection<string>(objectives));
    }
}
