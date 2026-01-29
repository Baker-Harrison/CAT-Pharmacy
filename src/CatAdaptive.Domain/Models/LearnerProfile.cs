namespace CatAdaptive.Domain.Models;

public sealed record LearnerProfile(
    Guid Id,
    string Name,
    IReadOnlyList<string> Objectives)
{
    public static LearnerProfile Create(string name, IEnumerable<string>? objectives = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        var goals = objectives?.Where(goal => !string.IsNullOrWhiteSpace(goal))
                      .Select(goal => goal.Trim())
                      .ToList() ?? new List<string>();

        return new LearnerProfile(Guid.NewGuid(), name.Trim(), goals);
    }
}
