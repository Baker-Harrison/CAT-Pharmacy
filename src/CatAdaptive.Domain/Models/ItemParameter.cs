using System;

namespace CatAdaptive.Domain.Models;

public sealed record ItemParameter(double Difficulty, double Discrimination = 1.0, double Guessing = 0.2)
{
    private const double D = 1.7; // Scaling constant for logistic approximation

    public double ProbabilityCorrect(double theta)
    {
        var exponent = -D * Discrimination * (theta - Difficulty);
        var logistic = 1.0 / (1.0 + Math.Exp(exponent));
        return Guessing + (1 - Guessing) * logistic;
    }

    public double FisherInformation(double theta)
    {
        var p = ProbabilityCorrect(theta);
        var q = 1 - p;
        var numerator = Math.Pow(D * Discrimination, 2) * Math.Pow(p - Guessing, 2);
        var denominator = Math.Pow(1 - Guessing, 2) * p * q;
        return denominator <= 0 ? 0 : numerator * q / (p * (1 - Guessing));
    }
}
