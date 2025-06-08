namespace Dawn.Apps.ToggleRoundedCorners.Services;

using global::Serilog;
using global::Dawn.Libs.ToggleRoundedCorners.Native;
using Interfaces;

internal sealed class CornerService(ILogger logger) : ICornerService
{
    private readonly DWMInteraction _dwmInteraction = new(logger);
    
    private readonly SemaphoreSlim _initializeSemaphore = new(1, 1);
    private SuccessResult? _cachedInitializeResult;

    public async Task<SuccessResult> InitializeIfNecessary() => await Initialize(true);

    private async Task<SuccessResult> Initialize(bool shouldWait)
    {
        // Windows 11
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return SuccessResult.Failed with { Exception = new PlatformNotSupportedException("Windows 11 or greater is required to run this app") };
        
        if (shouldWait)
            await _initializeSemaphore.WaitAsync();
        try
        {
            if (_cachedInitializeResult is { Success: true })
                return _cachedInitializeResult.Value;

            var result = await Task.Run(async () => await _dwmInteraction.Initialize());

            _cachedInitializeResult = result;
            return result;
        }
        finally
        {
            if (shouldWait)
                _initializeSemaphore.Release();
        }
    }
    
    public async Task<SuccessResult> ResetToDefault()
    {
        var result = await InitializeIfNecessary();
        if (!result.Success)
            return SuccessResult.Failed with { Exception = new AggregateException("Failed to Initialize", result.Exception!) };

        return _dwmInteraction.ResetToDefault();
    }

    public async Task<SuccessResult> ToggleRoundedCorners()
    {
        var result = await InitializeIfNecessary();
        if (!result.Success)
            return SuccessResult.Failed with { Exception = new AggregateException("Failed to Initialize", result.Exception!) };
        
        return _dwmInteraction.ToggleRoundedCorners();
    }

    public async Task<SuccessResult> SetSharpCorners()
    {
        var result = await InitializeIfNecessary();
        if (!result.Success)
            return SuccessResult.Failed with { Exception = new AggregateException("Failed to Initialize", result.Exception!) };
        
        return _dwmInteraction.SetSharpCorners();
    }

    public Task<SuccessResult> SetRoundedCorners() => ResetToDefault();
    public async Task<SuccessResult> RestartDWM()
    {
        var result = DWMInteraction.GetDWMProcess(logger, out _, out var proc);
        if (!result.Success)
            return result;

        proc.Kill();
        
        await proc.WaitForExitAsync();
        await Task.Delay(TimeSpan.FromSeconds(1)); // Wait for it to start up
        return await ClearCache();
    }

    public async Task<SuccessResult> ClearCache()
    {
        await _initializeSemaphore.WaitAsync();
        try
        {
            _cachedInitializeResult = null;
            
            if (_dwmInteraction.CachePath.Exists)
                _dwmInteraction.CachePath.Delete(true);

            // Commented this out as a clear cache method shouldn't refresh the cache
            // return await Task.Run(async () => await Internal_InitializeIfNecessary(false));
            return true;
        }
        catch (Exception e)
        {
            return SuccessResult.Failed with { Exception = e };
        }
        finally
        {
            _initializeSemaphore.Release();
        }
    }

    public void SubscribeProgressUpdates(Action<string> callback)
    {
        
    }
}