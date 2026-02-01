using System.IO;
using CatAdaptive.App.Services;
using CatAdaptive.App.ViewModels;
using CatAdaptive.Application.Abstractions;
using CatAdaptive.Application.Services;
using CatAdaptive.Infrastructure.Generation;
using CatAdaptive.Infrastructure.Parsing;
using CatAdaptive.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CatAdaptive.App;

public static class ServiceCollectionExtensions
{
    public static void ConfigureAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatAdaptive",
            "data");

        // Core repositories
        services.AddSingleton<IItemRepository>(new JsonItemRepository(dataDirectory));
        services.AddSingleton<IKnowledgeUnitRepository>(new JsonKnowledgeUnitRepository(dataDirectory));
        services.AddSingleton<ILessonPlanRepository>(new JsonLessonPlanRepository(dataDirectory));
        services.AddSingleton<ISessionRepository>(new InMemorySessionRepository());
        services.AddSingleton<IPptxParser, PptxParser>();
        services.AddSingleton<IDialogService, DialogService>();

        // Personalized learning repositories
        services.AddSingleton<IStudentStateRepository>(sp =>
            new JsonStudentStateRepository(dataDirectory, sp.GetRequiredService<ILogger<JsonStudentStateRepository>>()));
        services.AddSingleton<IAIContentGraphRepository>(sp =>
            new JsonAIContentGraphRepository(dataDirectory, sp.GetRequiredService<ILogger<JsonAIContentGraphRepository>>()));
        services.AddSingleton<ILearningObjectiveMapRepository>(sp =>
            new JsonLearningObjectiveMapRepository(dataDirectory, sp.GetRequiredService<ILogger<JsonLearningObjectiveMapRepository>>()));
        services.AddSingleton<IDomainGraphRepository>(sp =>
            new JsonDomainGraphRepository(dataDirectory, sp.GetRequiredService<ILogger<JsonDomainGraphRepository>>()));

        // Gemini configuration
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        }

        var modelName = configuration["Gemini:ModelName"] ?? "gemini-2.0-flash-exp";
        var useGemini = configuration.GetValue<bool>("Gemini:UseGemini");

        if (useGemini)
        {
            services.AddSingleton<IItemGenerator>(new GeminiItemGenerator(apiKey, modelName));
        }
        else
        {
            services.AddSingleton<IItemGenerator, SimpleItemGenerator>();
        }

        services.AddSingleton<ILessonPlanGenerator>(new GeminiLessonPlanGenerator(apiKey, modelName));
        services.AddSingleton<ILessonQuizEvaluator>(new GeminiLessonQuizEvaluator(apiKey, modelName));

        // NEW: Gemini service for personalized learning
        services.AddSingleton<IGeminiService>(sp =>
            new GeminiService(apiKey, modelName, sp.GetRequiredService<ILogger<GeminiService>>()));

        // Core services
        services.AddSingleton<ContentIngestionService>();
        services.AddSingleton<AdaptiveTestService>();

        // Personalized learning services
        services.AddSingleton<StudentStateService>();
        services.AddSingleton<AIContentExpansionService>();
        services.AddSingleton<ToTContentGenerator>();
        services.AddSingleton<PersonalizedLearningOrchestrator>();

        // ViewModels
        services.AddSingleton<UploadViewModel>();
        services.AddSingleton<LessonsViewModel>();
        services.AddSingleton<PersonalizedLearningViewModel>();
        services.AddSingleton<DebugViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
