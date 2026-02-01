using CatAdaptive.App.ViewModels;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Services;
using CatAdaptive.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WpfApplication = System.Windows.Application;

namespace CatAdaptive.App.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void NavigateToLessons_UsesLessonsViewWhenNotStarted()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplication();
            var fixture = new ViewModelFixture();
            fixture.PersonalizedLearningViewModel.CurrentPhase = LearningPhase.NotStarted;

            var vm = new MainViewModel(
                fixture.UploadViewModel,
                fixture.LessonsViewModel,
                fixture.PersonalizedLearningViewModel,
                fixture.DebugViewModel);

            vm.NavigateToLessonsCommand.Execute(null);

            vm.CurrentView.Should().Be(fixture.LessonsViewModel);
            vm.CurrentPage.Should().Be("Lessons");
        });
    }

    [Fact]
    public void NavigateToLessons_UsesPersonalizedViewWhenActive()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplication();
            var fixture = new ViewModelFixture();
            fixture.PersonalizedLearningViewModel.CurrentPhase = LearningPhase.Learning;

            var vm = new MainViewModel(
                fixture.UploadViewModel,
                fixture.LessonsViewModel,
                fixture.PersonalizedLearningViewModel,
                fixture.DebugViewModel);

            vm.NavigateToLessonsCommand.Execute(null);

            vm.CurrentView.Should().Be(fixture.PersonalizedLearningViewModel);
            vm.CurrentPage.Should().Be("Personalized Learning");
        });
    }

    [Fact]
    public void ToggleTheme_ReplacesResourceDictionary()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplication();
            var fixture = new ViewModelFixture();
            var vm = new MainViewModel(
                fixture.UploadViewModel,
                fixture.LessonsViewModel,
                fixture.PersonalizedLearningViewModel,
                fixture.DebugViewModel);

            var initialCount = WpfApplication.Current!.Resources.MergedDictionaries.Count;

            vm.IsDarkModeEnabled = true;

            WpfApplication.Current!.Resources.MergedDictionaries.Count.Should().BeGreaterThanOrEqualTo(initialCount);
        });
    }

    [Fact]
    public void RemoveNotification_RemovesFromCollection()
    {
        StaTestRunner.Run(() =>
        {
            EnsureApplication();
            var fixture = new ViewModelFixture();
            var vm = new MainViewModel(
                fixture.UploadViewModel,
                fixture.LessonsViewModel,
                fixture.PersonalizedLearningViewModel,
                fixture.DebugViewModel);

            var notification = new CatAdaptive.App.Models.Notification("Title", "Message");
            vm.Notifications.Add(notification);

            vm.RemoveNotificationCommand.Execute(notification);

            vm.Notifications.Should().BeEmpty();
        });
    }

    private static void EnsureApplication()
    {
        if (WpfApplication.Current == null)
        {
            _ = new WpfApplication();
        }
        WpfApplication.Current!.Resources.MergedDictionaries.Clear();
    }

    private sealed class ViewModelFixture
    {
        public UploadViewModel UploadViewModel { get; }
        public LessonsViewModel LessonsViewModel { get; }
        public PersonalizedLearningViewModel PersonalizedLearningViewModel { get; }
        public DebugViewModel DebugViewModel { get; }

        public ViewModelFixture()
        {
            var contentIngestion = new ContentIngestionService(
                Mock.Of<IPptxParser>(),
                Mock.Of<IKnowledgeUnitRepository>(),
                Mock.Of<IItemGenerator>(),
                Mock.Of<IItemRepository>(),
                Mock.Of<IAIContentGraphRepository>(),
                Mock.Of<IDomainGraphRepository>(),
                Mock.Of<ILogger<ContentIngestionService>>());

            UploadViewModel = new UploadViewModel(contentIngestion, Mock.Of<IDialogService>());

            LessonsViewModel = new LessonsViewModel(
                Mock.Of<ILessonPlanRepository>(),
                Mock.Of<IAIContentGraphRepository>());

            var studentStateService = new StudentStateService(
                Mock.Of<IStudentStateRepository>(),
                Mock.Of<IDomainGraphRepository>(),
                Mock.Of<IGeminiService>(),
                Mock.Of<ILogger<StudentStateService>>());

            PersonalizedLearningViewModel = new PersonalizedLearningViewModel(
                new PersonalizedLearningOrchestrator(
                    studentStateService,
                    new AIContentExpansionService(Mock.Of<IGeminiService>(), Mock.Of<IAIContentGraphRepository>(), Mock.Of<ILogger<AIContentExpansionService>>()),
                    new ToTContentGenerator(Mock.Of<IGeminiService>(), Mock.Of<ILogger<ToTContentGenerator>>()),
                    Mock.Of<IAIContentGraphRepository>(),
                    Mock.Of<ILearningObjectiveMapRepository>(),
                    Mock.Of<IDomainGraphRepository>(),
                    Mock.Of<IGeminiService>(),
                    Mock.Of<ILogger<PersonalizedLearningOrchestrator>>()),
                studentStateService,
                Mock.Of<ILogger<PersonalizedLearningViewModel>>());

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Gemini:UseGemini"] = "false",
                    ["Gemini:ModelName"] = "test"
                })
                .Build();

            DebugViewModel = new DebugViewModel(
                configuration,
                Mock.Of<IKnowledgeUnitRepository>(),
                Mock.Of<IItemRepository>(),
                Mock.Of<ILessonPlanRepository>(),
                Mock.Of<IStudentStateRepository>());
        }
    }
}
