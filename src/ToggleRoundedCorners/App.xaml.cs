using System.Windows;
using Serilog.Core;

namespace Dawn.Apps.ToggleRoundedCorners;

using System.Diagnostics;
using System.IO;
using global::Serilog;
using global::Serilog.Events;
using global::Dawn.Libs.ToggleRoundedCorners.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32.TaskScheduler;
using Serilog.CustomEnrichers;
using Services;
using Services.Interfaces;
using SoundOverlay.RW.Themes;
using Tools;
using ViewModels;
using Application = System.Windows.Forms.Application;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{

    public static IServiceProvider Services { get; private set; } = null!;
    
    public static LaunchArgs  Arguments { get; private set; }
    
    
    private const string LOGGING_FORMAT = "{Level:u1} {Timestamp:yyyy-MM-dd HH:mm:ss.ffffff}   [{Source}] {Message:lj}{NewLine}{Exception}";
    private static ServiceProvider ConfigureServices(ServiceCollection collection)
    {
        collection.AddSerilog(config =>
        {
            config
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .Enrich.With<ClassNameEnricher>()
                .Enrich.WithProcessName()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    theme: SerilogBlizzardTheme.GetTheme,
                    outputTemplate: LOGGING_FORMAT,
                    applyThemeToRedirectedOutput: true
                );
            
            if (!Arguments.NoFileLogging)
                config.WriteTo.File(Path.Combine(AppContext.BaseDirectory, $"{Application.ProductName}.log"),
                    outputTemplate: LOGGING_FORMAT,
                    restrictedToMinimumLevel: Arguments.ExtendedLogging
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information,
                    retainedFileCountLimit: 1,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: (long)Math.Pow(1024, 2) * 20, // 20mb
                    flushToDiskInterval: TimeSpan.FromSeconds(1));
            
            #if RELEASE
            config.WriteTo.Seq(Arguments.HasCustomSeqUrl ? Arguments.CustomSeqUrl : "http://localhost:9999");
            #endif
        });

        collection.AddSingleton<GUISink>();

        collection.AddSingleton<IWindowService, WindowService>();
        collection.AddSingleton<ICornerService, CornerService>(services =>
        {
            var logger = services.GetRequiredService<GUISink>();
            return new CornerService(logger);
        });
        collection.AddSingleton<ITaskSchedulerService, AdminTaskScheduler>();
        
        collection.AddSingleton<TaskService>(_ => TaskService.Instance);
        
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<MainView>();

        return collection.BuildServiceProvider();
    }
    
    private static void InitConsole()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, boxedException) =>
        {
            Debugger.Break();
            Log.Error((boxedException.ExceptionObject as Exception)!, "Unhandled Exception");
        };
        TaskScheduler.UnobservedTaskException += (_, args) => Log.Error(args.Exception, "Unobserved Task Exception");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        Arguments = new LaunchArgs(e.Args);
        
        InitConsole();
        Services = ConfigureServices([]);
        
        Log.Logger = Services.GetRequiredService<ILogger>();
        Log.Information("Initialized {ApplicationName} on version {ApplicationVersion} ({WindowsVersion})", Application.ProductName, Application.ProductVersion, Environment.OSVersion.Version);
        Log.Verbose("Process Arguments are {Arguments}", Arguments.RawArgs);
        
        var taskScheduler = Services.GetRequiredService<ITaskSchedulerService>();
        var taskSchedulerKey = Application.ProductName!;
        if (!PlatformTools.IsAdmin())
        {
            if (!taskScheduler.RunIfPresent(taskSchedulerKey)) // Task Scheduler doesn't show a UAC prompt (-1 person pissed off due to UAC)
                PlatformTools.StartAsAdmin(Arguments.CommandLine);
            
            Shutdown(2);
            return;
        }

        if (Arguments.Headless)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                if (Arguments.UseSharpCorners)
                {
                    var result = await Services.GetRequiredService<ICornerService>().SetSharpCorners();

                    if (!result.Success) 
                        Log.Error(result.Exception!, "Could not set sharp corners from headless mode");
                    else
                        Log.Information("Set sharp corners from headless mode");
                }

                Shutdown(0);
            });
        }
        else
        {
            taskScheduler.AddIfEnabled(taskSchedulerKey, Arguments.CommandLine);
            
            if (Arguments.HasProcessBinding)
                _ = new ProcessBinding(Arguments.ProcessBinding, Services.GetRequiredService<ILogger>());
            
            var windowService = Services.GetRequiredService<IWindowService>();
            windowService.ShowWindow<MainView>();

            if (Arguments.UseSharpCorners) 
                Services.GetRequiredService<MainViewModel>().UseSharpCornersCommand?.Execute(null);
            
            
            base.OnStartup(e);

        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}