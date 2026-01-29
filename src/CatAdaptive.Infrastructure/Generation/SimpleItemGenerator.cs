using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Infrastructure.Generation;

public sealed class SimpleItemGenerator : IItemGenerator
{
    private readonly Random _random = new();

    public Task<IReadOnlyList<ItemTemplate>> GenerateItemsAsync(
        IEnumerable<KnowledgeUnit> knowledgeUnits,
        CancellationToken ct = default)
    {
        var items = new List<ItemTemplate>();

        foreach (var unit in knowledgeUnits)
        {
            if (unit.KeyPoints.Count == 0) continue;

            foreach (var keyPoint in unit.KeyPoints)
            {
                if (string.IsNullOrWhiteSpace(keyPoint) || keyPoint.Length < 10) continue;

                var item = GenerateMultipleChoiceItem(unit, keyPoint);
                items.Add(item);
            }
        }

        return Task.FromResult<IReadOnlyList<ItemTemplate>>(items);
    }

    private ItemTemplate GenerateMultipleChoiceItem(KnowledgeUnit unit, string keyPoint)
    {
        var stem = $"Which of the following is true regarding {unit.Topic}?";

        var correctChoice = ItemChoice.Create(keyPoint, isCorrect: true);
        var distractors = GenerateDistractors(keyPoint, 3);

        var choices = new List<ItemChoice> { correctChoice };
        choices.AddRange(distractors);
        choices = choices.OrderBy(_ => _random.Next()).ToList();

        var difficulty = _random.NextDouble() * 2 - 1;
        var discrimination = 0.8 + _random.NextDouble() * 0.8;
        var parameter = new ItemParameter(difficulty, discrimination, 0.2);

        return ItemTemplate.Create(
            stem: stem,
            choices: choices,
            format: ItemFormat.MultipleChoice,
            parameter: parameter,
            knowledgeUnitIds: new[] { unit.Id },
            topic: unit.Topic,
            subtopic: unit.Subtopic,
            explanation: $"This is based on the content: {keyPoint}");
    }

    private List<ItemChoice> GenerateDistractors(string correctAnswer, int count)
    {
        var distractors = new List<ItemChoice>();
        var templates = new[]
        {
            "This is not accurate according to the source material.",
            "This statement contradicts the main concept.",
            "This option describes a different process entirely.",
            "This is a common misconception about the topic."
        };

        for (var i = 0; i < count && i < templates.Length; i++)
        {
            distractors.Add(ItemChoice.Create(templates[i], isCorrect: false));
        }

        return distractors;
    }
}
