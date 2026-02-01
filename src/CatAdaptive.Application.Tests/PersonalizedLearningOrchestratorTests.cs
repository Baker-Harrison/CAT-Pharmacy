using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Services;
using CatAdaptive.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CatAdaptive.Application.Tests;

public sealed class PersonalizedLearningOrchestratorTests
{
    [Fact]
    public async Task StartPersonalizedSessionAsync_CreatesSessionWithModules()
    {
        var fixture = new OrchestratorFixture();

        var session = await fixture.Orchestrator.StartPersonalizedSessionAsync(
            fixture.StudentId,
            new LearningGoals(MaxModules: 3));

        session.Status.Should().Be(SessionStatus.Active);
        session.LearningPath.Modules.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCurrentModuleContentAsync_ReturnsPersonalizedContent()
    {
        var fixture = new OrchestratorFixture();
        var session = await fixture.Orchestrator.StartPersonalizedSessionAsync(
            fixture.StudentId,
            new LearningGoals(MaxModules: 1));

        var content = await fixture.Orchestrator.GetCurrentModuleContentAsync(session);

        content.PersonalizedContent.Should().NotBeEmpty();
        content.AssessmentQuestions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessInteractionAsync_RemediatesWhenScoreLow()
    {
        var fixture = new OrchestratorFixture();
        var session = await fixture.Orchestrator.StartPersonalizedSessionAsync(
            fixture.StudentId,
            new LearningGoals(MaxModules: 1));

        var module = session.LearningPath.GetCurrentModule();
        module.Should().NotBeNull();

        var interaction = new StudentInteraction(
            NodeId: module!.DomainNodeId,
            Response: "response",
            ExpectedAnswer: "expected",
            CorrectnesScore: 0.2,
            Confidence: 0.5,
            ResponseTimeSeconds: 20,
            Difficulty: module.Difficulty,
            BloomsLevel: BloomsLevel.Remember,
            ContentType: "question",
            AttemptNumber: 1);

        var response = await fixture.Orchestrator.ProcessInteractionAsync(session, interaction);

        response.RecommendedAction.Should().Be("remediate");
        response.ShouldAdvance.Should().BeFalse();
        response.NextContent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompleteModuleAsync_ReturnsPassAndNextModule()
    {
        var fixture = new OrchestratorFixture();
        var session = await fixture.Orchestrator.StartPersonalizedSessionAsync(
            fixture.StudentId,
            new LearningGoals(MaxModules: 2));

        var module = session.LearningPath.GetCurrentModule();
        module.Should().NotBeNull();

        fixture.SetMastery(module!.DomainNodeId, MasteryLevel.Proficient, 0.8);

        var result = await fixture.Orchestrator.CompleteModuleAsync(session, module.ModuleId);

        result.Passed.Should().BeTrue();
        result.RecommendedNextModule.Should().NotBeNull();
    }

    private sealed class OrchestratorFixture
    {
        private readonly Mock<IStudentStateRepository> _studentRepository = new();
        private StudentStateModel? _state;
        private readonly DomainKnowledgeGraph _domainGraph;
        private readonly AIEnhancedContentGraph _contentGraph;
        private readonly LearningObjectiveMap _objectiveMap;

        public Guid StudentId { get; } = Guid.NewGuid();
        public PersonalizedLearningOrchestrator Orchestrator { get; }

        public OrchestratorFixture()
        {
            var nodeId = Guid.NewGuid();
            _domainGraph = new DomainKnowledgeGraph();
            _domainGraph.AddNode(new DomainNode(
                nodeId,
                "Node",
                "Desc",
                DomainNodeType.Concept,
                BloomsLevel.Understand,
                0.4,
                0.9,
                new[] { "tag" }));

            _contentGraph = new AIEnhancedContentGraph();
            _contentGraph.AddNode(new ContentNode(
                Guid.NewGuid(),
                ContentNodeType.Explanation,
                "Node",
                "Content",
                ContentModality.Text,
                BloomsLevel.Understand,
                0.3,
                5,
                new[] { nodeId },
                new[] { "tag" },
                ContentOrigin.Slides,
                0.9,
                DateTimeOffset.UtcNow));
            _contentGraph.AddNode(new ContentNode(
                Guid.NewGuid(),
                ContentNodeType.Question,
                "Question",
                "Q?",
                ContentModality.Text,
                BloomsLevel.Understand,
                0.3,
                3,
                new[] { nodeId },
                new[] { "tag" },
                ContentOrigin.Slides,
                0.9,
                DateTimeOffset.UtcNow));

            _objectiveMap = new LearningObjectiveMap();
            var objective = new LearningObjective(Guid.NewGuid(), Guid.NewGuid(), "Objective", BloomsLevel.Understand, "Topic", new[] { "tag" });
            _objectiveMap.AddObjective(objective);
            _objectiveMap.LinkToDomainNode(objective.Id, nodeId);

            _studentRepository.Setup(r => r.GetByStudentAsync(StudentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _state);
            _studentRepository.Setup(r => r.SaveAsync(It.IsAny<StudentStateModel>(), It.IsAny<CancellationToken>()))
                .Callback<StudentStateModel, CancellationToken>((state, _) => _state = state)
                .Returns(Task.CompletedTask);

            var graphRepository = new Mock<IDomainGraphRepository>();
            graphRepository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_domainGraph);

            var contentRepository = new Mock<IAIContentGraphRepository>();
            contentRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_contentGraph);

            var objectiveRepository = new Mock<ILearningObjectiveMapRepository>();
            objectiveRepository.Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_objectiveMap);

            var gemini = new Mock<IGeminiService>();
            gemini.Setup(g => g.GenerateThoughtPathsAsync(It.IsAny<string>(), 4, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "path1", "path2", "path3", "path4" });
            gemini.Setup(g => g.SelectBestPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("best path");
            gemini.Setup(g => g.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("content");

            var studentStateService = new StudentStateService(
                _studentRepository.Object,
                graphRepository.Object,
                gemini.Object,
                Mock.Of<ILogger<StudentStateService>>());

            var contentGenerator = new ToTContentGenerator(gemini.Object, Mock.Of<ILogger<ToTContentGenerator>>());
            var contentExpander = new AIContentExpansionService(gemini.Object, contentRepository.Object, Mock.Of<ILogger<AIContentExpansionService>>());

            Orchestrator = new PersonalizedLearningOrchestrator(
                studentStateService,
                contentExpander,
                contentGenerator,
                contentRepository.Object,
                objectiveRepository.Object,
                graphRepository.Object,
                gemini.Object,
                Mock.Of<ILogger<PersonalizedLearningOrchestrator>>());
        }

        public void SetMastery(Guid nodeId, MasteryLevel level, double confidence)
        {
            _state ??= new StudentStateModel(StudentId);
            _state.UpdateKnowledgeMastery(new KnowledgeMastery(
                nodeId,
                level,
                confidence,
                1,
                DateTimeOffset.UtcNow,
                Array.Empty<RetrievalEvent>(),
                Array.Empty<KnowledgeGap>(),
                0.9,
                EvidenceVector.Empty));
        }
    }
}
