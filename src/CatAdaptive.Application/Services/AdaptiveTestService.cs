using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Aggregates;
using CatAdaptive.Domain.Models;

namespace CatAdaptive.Application.Services;

public sealed class AdaptiveTestService
{
    private readonly IItemRepository _itemRepository;
    private readonly ISessionRepository _sessionRepository;

    public AdaptiveTestService(
        IItemRepository itemRepository,
        ISessionRepository sessionRepository)
    {
        _itemRepository = itemRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<AdaptiveSession> StartSessionAsync(
        LearnerProfile learner,
        string? topic = null,
        TerminationCriteria? criteria = null,
        CancellationToken ct = default)
    {
        var items = string.IsNullOrWhiteSpace(topic)
            ? await _itemRepository.GetAllAsync(ct)
            : await _itemRepository.GetByTopicAsync(topic, ct);

        if (items.Count == 0)
        {
            throw new InvalidOperationException("No items available for the selected topic.");
        }

        var session = new AdaptiveSession(
            Guid.NewGuid(),
            learner,
            items,
            criteria ?? TerminationCriteria.Default());

        await _sessionRepository.SaveAsync(session, ct);
        return session;
    }

    public ItemTemplate? GetNextItem(AdaptiveSession session)
    {
        return session.AdvanceToNextItem();
    }

    public async Task<ItemResponse> SubmitResponseAsync(
        AdaptiveSession session,
        bool isCorrect,
        TimeSpan responseTime,
        string rawResponse,
        CancellationToken ct = default)
    {
        var response = session.RecordResponse(isCorrect, responseTime, rawResponse);
        await _sessionRepository.SaveAsync(session, ct);
        return response;
    }

    public SessionReport GenerateReport(AdaptiveSession session)
    {
        var topicPerformance = session.Responses
            .GroupBy(r => r.ItemTemplateId)
            .ToDictionary(g => g.Key, g => g.Average(r => r.Score));

        var correctCount = session.Responses.Count(r => r.IsCorrect);
        var totalCount = session.Responses.Count;

        return new SessionReport(
            session.Id,
            session.Learner.Name,
            session.CurrentAbility.Theta,
            session.CurrentAbility.StandardError,
            correctCount,
            totalCount,
            session.IsComplete,
            topicPerformance);
    }
}

public sealed record SessionReport(
    Guid SessionId,
    string LearnerName,
    double FinalAbility,
    double StandardError,
    int CorrectResponses,
    int TotalResponses,
    bool IsComplete,
    IReadOnlyDictionary<Guid, double> TopicPerformance)
{
    public double AccuracyPercent => TotalResponses > 0 ? (double)CorrectResponses / TotalResponses * 100 : 0;
}
