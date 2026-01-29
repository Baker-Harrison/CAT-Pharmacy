using System.Collections.ObjectModel;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Domain.Aggregates;

public sealed class AdaptiveSession
{
    private readonly List<ItemTemplate> _remainingItems;
    private readonly List<ItemResponse> _responses = new();

    public AdaptiveSession(
        Guid id,
        LearnerProfile learner,
        IEnumerable<ItemTemplate> itemPool,
        TerminationCriteria criteria,
        AbilityEstimate? initialAbility = null)
    {
        Id = id;
        Learner = learner ?? throw new ArgumentNullException(nameof(learner));
        _remainingItems = itemPool?.ToList() ?? throw new ArgumentNullException(nameof(itemPool));
        Criteria = criteria ?? throw new ArgumentNullException(nameof(criteria));
        CurrentAbility = initialAbility ?? AbilityEstimate.Initial();
    }

    public Guid Id { get; }
    public LearnerProfile Learner { get; }
    public TerminationCriteria Criteria { get; }
    public AbilityEstimate CurrentAbility { get; private set; }
    public ItemTemplate? ActiveItem { get; private set; }
    public IReadOnlyList<ItemResponse> Responses => new ReadOnlyCollection<ItemResponse>(_responses);
    public bool IsComplete { get; private set; }

    public ItemTemplate? AdvanceToNextItem()
    {
        if (IsComplete)
        {
            ActiveItem = null;
            return null;
        }

        if (_remainingItems.Count == 0)
        {
            IsComplete = true;
            ActiveItem = null;
            return null;
        }

        ActiveItem = _remainingItems
            .OrderByDescending(item => item.Parameter.FisherInformation(CurrentAbility.Theta))
            .First();

        _remainingItems.Remove(ActiveItem);
        return ActiveItem;
    }

    public ItemResponse RecordResponse(bool isCorrect, TimeSpan responseTime, string rawResponse)
    {
        if (ActiveItem is null)
        {
            throw new InvalidOperationException("Cannot record a response without an active item.");
        }

        var score = isCorrect ? 1.0 : 0.0;
        var updatedAbility = UpdateAbilityEstimate(ActiveItem, isCorrect);
        CurrentAbility = updatedAbility;

        var response = ItemResponse.Create(
            Guid.NewGuid(),
            ActiveItem.Id,
            isCorrect,
            score,
            responseTime,
            rawResponse,
            updatedAbility);

        _responses.Add(response);
        ActiveItem = null;

        if (ShouldTerminate())
        {
            IsComplete = true;
        }

        return response;
    }

    private AbilityEstimate UpdateAbilityEstimate(ItemTemplate item, bool isCorrect)
    {
        var theta = CurrentAbility.Theta;
        var probability = item.Parameter.ProbabilityCorrect(theta);
        var score = isCorrect ? 1.0 : 0.0;
        var info = Math.Max(item.Parameter.FisherInformation(theta), 1e-3);
        var gradient = score - probability;
        var step = gradient / info;

        var newTheta = Math.Clamp(theta + step, -3.0, 3.0);
        var standardError = 1.0 / Math.Sqrt(info);
        return new AbilityEstimate(Guid.NewGuid(), newTheta, standardError, "MLE", DateTime.UtcNow);
    }

    private bool ShouldTerminate()
    {
        if (_responses.Count >= Criteria.MaxItems)
        {
            return true;
        }

        if (CurrentAbility.StandardError <= Criteria.TargetStandardError)
        {
            return true;
        }

        if (Criteria.MasteryTheta.HasValue && CurrentAbility.Theta >= Criteria.MasteryTheta.Value)
        {
            return true;
        }

        return false;
    }
}
