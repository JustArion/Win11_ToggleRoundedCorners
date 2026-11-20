using System.Windows;
using Dawn.Common.Models;
using Dawn.Common.Serilog;
using Dawn.Common.Windows.Extensions;
using Serilog.Core;
using Vanara.PInvoke;

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
    
    
    private static ServiceProvider ConfigureServices(ServiceCollection collection)
    {
        collection.AddSerilog(config =>
        {
            var extendedLogging = Arguments.ExtendedLogging || Console.IsConsoleAttached;
            var logDir = new FileInfo(Environment.ProcessPath!).Directory ?? new DirectoryInfo(Environment.CurrentDirectory);

            config.AddCommon(new CommonLoggingOptions(true, !Arguments.NoFileLogging, extendedLogging,
                Arguments.CustomSeqUrl), logDir);
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
                    await WaitForDwmInit();
                    Log.Debug("DWM Ready!");
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

    private async Task WaitForDwmInit()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            Process? dwm;
            while ((dwm = Process.GetProcessesByName("dwm").FirstOrDefault()) is not { } proc || proc.HasExited) 
                await Task.Delay(10, cts.Token);

            if ((DateTime.Now - dwm.StartTime).TotalSeconds > 5)
                return; // We're the ones that's late, not DWM!

            Log.Verbose("Waiting for DWM to initialize!");
            if (WaitForInputIdle(dwm, (uint)TimeSpan.FromSeconds(5).TotalMilliseconds) is Kernel32.WAIT_STATUS.WAIT_OBJECT_0)
                return;
            
            Log.Error("Waiting for DWM to initialize took too long! We're calling it quits!");
            Shutdown(1);
        }
        catch (OperationCanceledException) when(cts.IsCancellationRequested)
        {
            Log.Error("Wait for DWM took too long! We're calling it quits!");
            Shutdown(2);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}