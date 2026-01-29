namespace CatAdaptive.Domain.Models;

public sealed record ItemResponse(
    Guid ItemId,
    Guid ItemTemplateId,
    bool IsCorrect,
    double Score,
    TimeSpan ResponseTime,
    string RawResponse,
    AbilityEstimate AbilityAfter)
{
    public static ItemResponse Create(
        Guid itemId,
        Guid templateId,
        bool isCorrect,
        double score,
        TimeSpan responseTime,
        string rawResponse,
        AbilityEstimate abilityAfter)
    {
        return new ItemResponse(
            itemId,
            templateId,
            isCorrect,
            score,
            responseTime,
            rawResponse,
            abilityAfter);
    }
}
