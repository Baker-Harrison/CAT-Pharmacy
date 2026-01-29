namespace CatAdaptive.Domain.Models;

public sealed record TerminationCriteria(
    double TargetStandardError,
    int MaxItems,
    double? MasteryTheta,
    int MaxStallCount)
{
    public static TerminationCriteria Default()
        => new(0.3, 25, 1.2, 3);
}
