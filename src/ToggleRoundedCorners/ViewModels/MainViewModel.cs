namespace Dawn.Apps.ToggleRoundedCorners.ViewModels;

using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using global::Serilog;
using Libs.ToggleRoundedCorners.Native;
using Services.Interfaces;
using Tools;
using Application = System.Windows.Forms.Application;

public partial class MainViewModel : ObservableObject
{
    private readonly ICornerService _cornerService = null!;
    private readonly ITaskSchedulerService _taskScheduler = null!;
    private readonly ILogger _logger = null!;
    private readonly string _taskSchedulerKey = $"{Application.ProductName!}_BackgroundTask";

    public MainViewModel(ICornerService cornerService, ITaskSchedulerService taskScheduler, ILogger logger)
    {
        _cornerService = cornerService;
        _taskScheduler = taskScheduler;
        _logger = logger;
        ToggleRoundedCornersCommand = new AsyncRelayCommand(ToggleRoundedCorners);
        UseRoundedCornersCommand = new AsyncRelayCommand(UseRoundedCorners);
        UseSharpCornersCommand = new AsyncRelayCommand(UseSharpCorners);
        RunOnStartupCommand = new RelayCommand(RunOnStartup);
        ClearCacheCommand = new AsyncRelayCommand(ClearCache);
        RestartDWMCommand = new AsyncRelayCommand(RestartDWM);
        
        _cornerService.SubscribeProgressUpdates(callback: s => logger.Information("Progress Update: {Update}", s));

        RunsOnStartup = taskScheduler.IsEnabled(_taskSchedulerKey);
    }

    private async Task RestartDWM()
    {
        var result = await AsyncOperation(_cornerService.RestartDWM());
        
        if (!result.Success)
            _errorCallbacks.ForEach(callback => callback("💀 " + result.Exception!.Message));
    }

    private async Task ClearCache()
    {
        var result = await AsyncOperation(_cornerService.ClearCache());
        if (!result.Success)
            _errorCallbacks.ForEach(callback => callback("💀 " + result.Exception!.Message));
        else
            _completionCallbacks.ForEach(callback => callback("Cache Cleared!"));
    }

    public void Initialize() => Task.Run(async () =>
    {
        var result = await AsyncOperation(_cornerService.InitializeIfNecessary());
        
        if (!result.Success)
            _errorCallbacks.ForEach(callback => callback("💀 " + result.Exception!.Message));
    });

    private readonly SemaphoreSlim _operationNotInProgressSemaphore = new(1, 1);
    private async Task<T> AsyncOperation<T>(Task<T> task)
    {
        await _operationNotInProgressSemaphore.WaitAsync();
        try
        {
            OperationNotInProgress = false;
            await task.WaitAsync(CancellationToken.None);
            OperationNotInProgress = true;
            
            return task.Result;
        }
        finally
        {
            _operationNotInProgressSemaphore.Release();
        }
    }
    
    public void SubscribeProgress(Action<string> callback) => _cornerService.SubscribeProgressUpdates(callback);

    private readonly List<Action<string>> _errorCallbacks = [];
    public void SubscribeError(Action<string> callback) => _errorCallbacks.Add(callback);

    private readonly List<Action<string>> _completionCallbacks = [];
    
    public void SubscribeCompletion(Action<string> callback) => _completionCallbacks.Add(callback);

    public MainViewModel()
    {
    }

    // The state is inverted due to the binding of RunsOnStartup, so the bool "RunsOnStartup" is our intent
    private void RunOnStartup()
    {
        try
        {
            if (RunsOnStartup)
                _taskScheduler.RunOnStartup(_taskSchedulerKey, LaunchArgs.USE_SHARP_CORNERS_ARGUMENT, LaunchArgs.HEADLESS_ARGUMENT);
            else
                _taskScheduler.Remove(_taskSchedulerKey);
        }
        catch (Exception e)
        {
            RunsOnStartup = false;
            _logger.Error(e, "Failed to set run on startup");
            _errorCallbacks.ForEach(callback => callback("💀 " + "Failed to set startup, consult the .log file for more information"));
        }

    }

    private async Task UseSharpCorners()
    {
        var result = await AsyncOperation(_cornerService.SetSharpCorners());
        if (!result.Success)
        {
            _logger.Error(result.Exception, "Failed to set sharp corners");
            _errorCallbacks.ForEach(callback => callback("💀 " + result.Exception!.Message));
        }
        else 
            _completionCallbacks.ForEach(callback => callback("Now using \"Sharp\" Corners!"));
    }

    private async Task UseRoundedCorners()
    {
        var result = await AsyncOperation(_cornerService.SetRoundedCorners());
        if (!result.Success)
        {
            _logger.Error(result.Exception, "Failed to set rounded corners");
            _errorCallbacks.ForEach(callback => callback("💀 " + result.Exception!.Message));
        }
        else
            _completionCallbacks.ForEach(callback => callback("Now using \"Rounded\" Corners!"));
    }

    private async Task ToggleRoundedCorners()
    {
        var result = await AsyncOperation(_cornerService.ToggleRoundedCorners());
        if (!result.Success)
        {
            _logger.Error(result.Exception, "Failed to toggle rounded corners");
            _errorCallbacks.ForEach(callback => callback("💀 " + result.Exception!.Message));
        }
        else
            _completionCallbacks.ForEach(callback => callback("Rounded Corners Toggled!"));
    }

    [ObservableProperty] 
    public partial bool OperationNotInProgress { get; set; } = true;
    
    [ObservableProperty]
    public partial bool RunsOnStartup { get; set; }
    
    [ObservableProperty]
    public partial ICommand? RestartDWMCommand { get; set; }
    [ObservableProperty]
    public partial ICommand? ClearCacheCommand { get; set; }

    [ObservableProperty] 
    public partial ICommand? RunOnStartupCommand { get; set; }

    [ObservableProperty] 
    public partial ICommand? ToggleRoundedCornersCommand { get; set; }
    
    [ObservableProperty] 
    public partial ICommand? UseRoundedCornersCommand { get; set; }
    
    [ObservableProperty] 
    public partial ICommand? UseSharpCornersCommand { get; set; }
}