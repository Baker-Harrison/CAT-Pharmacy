using System;

namespace CatAdaptive.Domain.Models;

public sealed record ItemParameter(double Difficulty, double Discrimination = 1.0, double Guessing = 0.2)
{
    private const double D = 1.7; // Scaling constant for logistic approximation
    private const double MaxExponent = 35.0;
    private const double MinProbability = 1e-9;

    public double ProbabilityCorrect(double theta)
    {
        var exponent = -D * Discrimination * (theta - Difficulty);
        var cappedExponent = Math.Clamp(exponent, -MaxExponent, MaxExponent);
        var logistic = 1.0 / (1.0 + Math.Exp(cappedExponent));
        return Guessing + (1 - Guessing) * logistic;
    }

    public double FisherInformation(double theta)
    {
        var p = ProbabilityCorrect(theta);
        var q = 1.0 - p;
        var oneMinusGuessing = 1.0 - Guessing;
        if (oneMinusGuessing <= 0)
        {
            return 0.0;
        }

        var clampedP = Math.Clamp(p, MinProbability, 1.0 - MinProbability);
        var clampedQ = 1.0 - clampedP;
        var scaledSlope = D * Discrimination;
        var normalizedP = (clampedP - Guessing) / oneMinusGuessing;
        return Math.Pow(scaledSlope, 2) * (clampedQ / clampedP) * Math.Pow(normalizedP, 2);
    }
}
