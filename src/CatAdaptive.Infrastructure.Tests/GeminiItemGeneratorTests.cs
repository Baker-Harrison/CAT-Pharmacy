using System.Reflection;
using CatAdaptive.Domain.Models;
using CatAdaptive.Infrastructure.Generation;
using FluentAssertions;

namespace CatAdaptive.Infrastructure.Tests;

public sealed class GeminiItemGeneratorTests
{
    [Fact]
    public void ParseGeminiResponse_ReturnsItemsForValidJson()
    {
        var method = typeof(GeminiItemGenerator)
            .GetMethod("ParseGeminiResponse", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var unit = new KnowledgeUnit(
            Guid.NewGuid(),
            "Topic",
            "Sub",
            "slide-1",
            "Summary",
            new[] { "Point" },
            new[] { "Objective" });

        var json = "[" +
                   "{\"stem\":\"Question?\"," +
                   "\"choices\":[{\"text\":\"A\",\"isCorrect\":true},{\"text\":\"B\",\"isCorrect\":false}]," +
                   "\"explanation\":\"Because\",\"difficulty\":0.2,\"bloomLevel\":\"Apply\",\"learningObjective\":\"Obj\"}" +
                   "]";

        var items = (List<ItemTemplate>)method!
            .Invoke(null, new object?[] { json, unit })!;

        items.Should().HaveCount(1);
        items[0].Choices.Should().HaveCount(2);
    }

    [Fact]
    public void ParseGeminiResponse_SkipsInvalidItems()
    {
        var method = typeof(GeminiItemGenerator)
            .GetMethod("ParseGeminiResponse", BindingFlags.NonPublic | BindingFlags.Static);

        var unit = new KnowledgeUnit(
            Guid.NewGuid(),
            "Topic",
            "Sub",
            "slide-1",
            "Summary",
            new[] { "Point" },
            new[] { "Objective" });

        var json = "[" +
                   "{\"stem\":\"\",\"choices\":[{\"text\":\"A\",\"isCorrect\":true}]}," +
                   "{\"stem\":\"Valid\",\"choices\":[{\"text\":\"A\",\"isCorrect\":true},{\"text\":\"B\",\"isCorrect\":false}]}" +
                   "]";

        var items = (List<ItemTemplate>)method!
            .Invoke(null, new object?[] { json, unit })!;

        items.Should().HaveCount(1);
        items[0].Stem.Should().Be("Valid");
    }
}
