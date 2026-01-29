using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CatAdaptive.App.ViewModels;
using CatAdaptive.Infrastructure.Generation;
using CatAdaptive.Infrastructure.Parsing;
using CatAdaptive.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AppAbstractions = CatAdaptive.Application.Abstractions;
using AppServices = CatAdaptive.Application.Services;

namespace CatAdaptive.App;

public partial class App : System.Windows.Application
{
    private readonly IServiceProvider _serviceProvider;

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    public App()
    {
        AllocConsole();
        Console.WriteLine("=== CAT Adaptive Study System - Debug Mode ===");
        RegisterCrashHandlers();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatAdaptive",
            "data");

        services.AddSingleton<AppAbstractions.IItemRepository>(new JsonItemRepository(dataDirectory));
        services.AddSingleton<AppAbstractions.IKnowledgeUnitRepository>(new JsonKnowledgeUnitRepository(dataDirectory));
        services.AddSingleton<AppAbstractions.IContentGraphRepository>(new JsonContentGraphRepository(dataDirectory));
        services.AddSingleton<AppAbstractions.IKnowledgeGraphRepository>(new JsonKnowledgeGraphRepository(dataDirectory));
        services.AddSingleton<AppAbstractions.ILessonPlanRepository>(new JsonLessonPlanRepository(dataDirectory));
        services.AddSingleton<AppAbstractions.ISessionRepository>(new InMemorySessionRepository());
        services.AddSingleton<AppAbstractions.IPptxParser, PptxParser>();

        var apiKey = configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var modelName = configuration["Gemini:ModelName"] ?? "gemini-2.0-flash-exp";
        var useGemini = configuration.GetValue<bool>("Gemini:UseGemini");

        if (useGemini)
        {
            services.AddSingleton<AppAbstractions.IItemGenerator>(new GeminiItemGenerator(apiKey, modelName));
        }
        else
        {
            services.AddSingleton<AppAbstractions.IItemGenerator, SimpleItemGenerator>();
        }

        services.AddSingleton<AppAbstractions.ILessonPlanGenerator>(new GeminiLessonPlanGenerator(apiKey, modelName));
        services.AddSingleton<AppAbstractions.ILessonQuizEvaluator>(new GeminiLessonQuizEvaluator(apiKey, modelName));

        services.AddSingleton<AppServices.LearningFlowService>();
        services.AddSingleton<AppServices.AdaptiveTestService>();

        services.AddSingleton<UploadViewModel>();
        services.AddSingleton<LessonsViewModel>();
        services.AddSingleton<AdaptiveSessionViewModel>();
        services.AddSingleton<DebugViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    private void RegisterCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            WriteCrashDetails(args.ExceptionObject as Exception, "AppDomain.UnhandledException");
            WaitForExit();
        };

        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashDetails(args.Exception, "DispatcherUnhandledException");
            args.Handled = true;
            WaitForExit();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashDetails(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
            WaitForExit();
        };
    }

    private static void WriteCrashDetails(Exception? exception, string source)
    {
        Console.WriteLine($"[{source}] Unhandled exception detected.");
        if (exception == null)
        {
            Console.WriteLine("Exception data was not available.");
            return;
        }

        Console.WriteLine(exception.ToString());
    }

    private static void WaitForExit()
    {
        Console.WriteLine("The application has crashed. Press Enter to exit.");
        try
        {
            Console.ReadLine();
        }
        catch
        {
            // Ignore console read failures.
        }
        Environment.Exit(1);
    }
}
