using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CatAdaptive.App.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        LoadEnvironmentVariables();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.ConfigureAppServices(configuration);
    }

    private static void LoadEnvironmentVariables()
    {
        var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../.env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
        }
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
