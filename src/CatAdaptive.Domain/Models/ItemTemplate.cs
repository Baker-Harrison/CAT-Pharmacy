using System.Collections.ObjectModel;

namespace CatAdaptive.Domain.Models;

public sealed record ItemTemplate(
    Guid Id,
    string Stem,
    IReadOnlyList<ItemChoice> Choices,
    ItemFormat Format,
    ItemParameter Parameter,
    IReadOnlyList<Guid> KnowledgeUnitIds,
    string Topic,
    string Subtopic,
    string Explanation,
    string BloomLevel,
    string LearningObjective,
    Guid? PrimaryConceptId,
    IReadOnlyList<Guid> SecondaryConceptIds)
{
    public static ItemTemplate Create(
        string stem,
        IEnumerable<ItemChoice> choices,
        ItemFormat format,
        ItemParameter parameter,
        IEnumerable<Guid> knowledgeUnitIds,
        string topic,
        string subtopic,
        string explanation,
        string bloomLevel = "Apply",
        string learningObjective = "",
        Guid? primaryConceptId = null,
        IEnumerable<Guid>? secondaryConceptIds = null)
    {
        if (string.IsNullOrWhiteSpace(stem))
        {
            throw new ArgumentException("Stem is required", nameof(stem));
        }

        var choiceList = choices?.ToList() ?? throw new ArgumentNullException(nameof(choices));
        if (choiceList.Count == 0 && format == ItemFormat.MultipleChoice)
        {
            throw new ArgumentException("Multiple choice items require at least one choice", nameof(choices));
        }

        var kuList = knowledgeUnitIds?.ToList() ?? new List<Guid>();
        var secondaryConceptList = secondaryConceptIds?.ToList() ?? new List<Guid>();

        return new ItemTemplate(
            Guid.NewGuid(),
            stem.Trim(),
            new ReadOnlyCollection<ItemChoice>(choiceList),
            format,
            parameter,
            new ReadOnlyCollection<Guid>(kuList),
            topic?.Trim() ?? string.Empty,
            subtopic?.Trim() ?? string.Empty,
            explanation?.Trim() ?? string.Empty,
            bloomLevel?.Trim() ?? "Apply",
            learningObjective?.Trim() ?? string.Empty,
            primaryConceptId,
            new ReadOnlyCollection<Guid>(secondaryConceptList));
    }
}
