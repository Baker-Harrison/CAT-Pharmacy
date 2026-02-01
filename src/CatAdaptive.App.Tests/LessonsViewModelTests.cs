using CatAdaptive.App.ViewModels;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using FluentAssertions;
using Moq;

namespace CatAdaptive.App.Tests;

public sealed class LessonsViewModelTests
{
    [Fact]
    public async Task LoadStatsAsync_SetsPropertiesWhenDataAvailable()
    {
        var lessons = new List<LessonPlan>
        {
            LessonPlan.Create(Guid.NewGuid(), "Title", "Summary", 5, false, Array.Empty<LessonSection>(), new LessonQuiz(Array.Empty<LessonQuizQuestion>()))
                .WithQuizResult(new LessonQuizResult(DateTimeOffset.UtcNow, 80, Array.Empty<LessonQuizQuestionResult>()))
        };

        var lessonRepo = new Mock<ILessonPlanRepository>();
        lessonRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lessons);

        var graph = new AIEnhancedContentGraph();
        graph.AddNode(new ContentNode(
            Guid.NewGuid(),
            ContentNodeType.Explanation,
            "Title",
            "Content",
            ContentModality.Text,
            BloomsLevel.Understand,
            0.3,
            5,
            new[] { Guid.NewGuid() },
            new[] { "tag" },
            ContentOrigin.Slides,
            0.9,
            DateTimeOffset.UtcNow));

        var graphRepo = new Mock<IAIContentGraphRepository>();
        graphRepo.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(graph);

        var vm = new LessonsViewModel(lessonRepo.Object, graphRepo.Object);

        await vm.LoadStatsAsync();

        vm.HasContent.Should().BeTrue();
        vm.HasLessons.Should().BeTrue();
        vm.CompletedLessonsCount.Should().Be(1);
        vm.AverageScore.Should().Be(80);
        vm.StatusMessage.Should().Contain("lesson");
    }

    [Fact]
    public async Task LoadStatsAsync_SetsStatusWhenNoContent()
    {
        var lessonRepo = new Mock<ILessonPlanRepository>();
        lessonRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<LessonPlan>());

        var graphRepo = new Mock<IAIContentGraphRepository>();
        graphRepo.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync((AIEnhancedContentGraph?)null);

        var vm = new LessonsViewModel(lessonRepo.Object, graphRepo.Object);

        await vm.LoadStatsAsync();

        vm.HasContent.Should().BeFalse();
        vm.StatusMessage.Should().Be("Upload lecture slides first to begin learning.");
    }

    [Fact]
    public void StartAdaptiveLessonCommand_CanExecuteWhenHasContent()
    {
        var vm = new LessonsViewModel(Mock.Of<ILessonPlanRepository>(), Mock.Of<IAIContentGraphRepository>());
        vm.HasContent = true;

        vm.StartAdaptiveLessonCommand.CanExecute(null).Should().BeTrue();
    }
}
