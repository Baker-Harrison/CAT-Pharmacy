namespace CatAdaptive.Domain.Models;

public sealed record ItemChoice(Guid Id, string Text, bool IsCorrect)
{
    public static ItemChoice Create(string text, bool isCorrect)
        => new(Guid.NewGuid(), text, isCorrect);
}
