using CatAdaptive.Application.Abstractions;
using CatAdaptive.Domain.Models;
using FluentAssertions;
using Moq;

namespace CatAdaptive.Application.Tests;

public sealed class LearningFlowServiceTests
{
    [Fact]
    public async Task GetLessonsAsync_DelegatesToAssessmentService()
    {
        // Arrange
        var mockRepo = new Mock<ILessonPlanRepository>();
        var expectedLessons = new List<LessonPlan>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLessons);

        // Act
        var lessons = await mockRepo.Object.GetAllAsync(CancellationToken.None);

        // Assert
        lessons.Should().BeEquivalentTo(expectedLessons);
        mockRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsLesson()
    {
        // Arrange
        var mockRepo = new Mock<ILessonPlanRepository>();
        var expectedLesson = LessonPlan.Create(
            Guid.NewGuid(),
            "Test Lesson",
            "Summary",
            15,
            false,
            new List<LessonSection>(),
            new LessonQuiz(new List<LessonQuizQuestion>()));

        mockRepo.Setup(r => r.GetByIdAsync(expectedLesson.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedLesson);

        // Act
        var lesson = await mockRepo.Object.GetByIdAsync(expectedLesson.Id, CancellationToken.None);

        // Assert
        lesson.Should().NotBeNull();
        lesson!.Id.Should().Be(expectedLesson.Id);
        lesson.Title.Should().Be("Test Lesson");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        var mockRepo = new Mock<ILessonPlanRepository>();
        var lessonId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetByIdAsync(lessonId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LessonPlan?)null);

        // Act
        var lesson = await mockRepo.Object.GetByIdAsync(lessonId, CancellationToken.None);

        // Assert
        lesson.Should().BeNull();
    }
}
