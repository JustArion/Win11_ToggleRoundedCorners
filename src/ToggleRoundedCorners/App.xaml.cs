using System.Windows;
using Dawn.Common.Models;
using Dawn.Common.Serilog;
using Dawn.Common.Windows.Extensions;
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
using Tools;
using ViewModels;
using Application = System.Windows.Forms.Application;
using Task = Task;

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
            var extendedLogging = Arguments.ExtendedLogging || Console.IsConsoleAttached;

            config.AddCommon(new CommonLoggingOptions(true, !Arguments.NoFileLogging, extendedLogging,
                Arguments.CustomSeqUrl));
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
        Log.Information("Logging to {Location}", Path.Combine(AppContext.BaseDirectory, $"{Application.ProductName}.log"));
        
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
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    var error = await Services.GetRequiredService<ICornerService>()
                        .SetSharpCorners();

                    if (error != null) 
                        Log.Error(error.ToException(), "Could not set sharp corners from headless mode");
                    else
                        Log.Information("Set sharp corners from headless mode");
                }

                await Log.CloseAndFlushAsync();
                await Task.Delay(TimeSpan.FromSeconds(3));
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