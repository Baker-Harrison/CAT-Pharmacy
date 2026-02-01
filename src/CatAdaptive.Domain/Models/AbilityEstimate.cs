namespace CatAdaptive.Domain.Models;

public sealed record AbilityEstimate(
    Guid Id,
    double Theta,
    double StandardError,
    string Method,
    DateTime Timestamp)
{
    public double Variance => StandardError * StandardError;

    public double Information => Variance <= 0 ? 0 : 1.0 / Variance;

    public static AbilityEstimate Initial(double theta = -1.5, double standardError = 1.0, string method = "Prior")
        => new(Guid.NewGuid(), theta, standardError, method, DateTime.UtcNow);
}
