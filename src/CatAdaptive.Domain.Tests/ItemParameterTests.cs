using CatAdaptive.Domain.Models;
using FluentAssertions;

namespace CatAdaptive.Domain.Tests;

public sealed class ItemParameterTests
{
    [Fact]
    public void ProbabilityCorrect_IncreasesWithHigherTheta()
    {
        var parameter = new ItemParameter(Difficulty: 0.0, Discrimination: 1.2, Guessing: 0.2);

        var low = parameter.ProbabilityCorrect(-1.0);
        var high = parameter.ProbabilityCorrect(1.0);

        high.Should().BeGreaterThan(low);
        low.Should().BeGreaterThanOrEqualTo(parameter.Guessing);
        high.Should().BeLessThan(1.0);
    }

    [Fact]
    public void FisherInformation_ReturnsZeroWhenGuessingIsOne()
    {
        var parameter = new ItemParameter(Difficulty: 0.0, Discrimination: 1.0, Guessing: 1.0);

        var info = parameter.FisherInformation(0.0);

        info.Should().Be(0.0);
    }
}
