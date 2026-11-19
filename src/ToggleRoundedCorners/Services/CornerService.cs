using Dawn.Common;

namespace Dawn.Apps.ToggleRoundedCorners.Services;

using global::Serilog;
using global::Dawn.Libs.ToggleRoundedCorners.Native;
using Interfaces;

/// <summary>
/// Service for managing rounded corners in the Windows Desktop Window Manager (DWM).
/// </summary>
internal sealed class CornerService(ILogger logger) : ICornerService
{
    private readonly DWMInteraction _dwmInteraction = new(logger);
    
    private readonly SemaphoreSlim _initializeSemaphore = new(1, 1);
    private Error? _cachedInitializeResult;
    private bool _isInitialized;

    public async Task<Error?> InitializeIfNecessary() => await Initialize(true);

    private async Task<Error?> Initialize(bool shouldWait)
    {
        // Windows 11
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return new PlatformNotSupportedException("Windows 11 or greater is required to run this app");
        
        if (shouldWait)
            await _initializeSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return _cachedInitializeResult;

            var result = await Task.Run(async () => await _dwmInteraction.Initialize());

            _cachedInitializeResult = result;
            _isInitialized = true;
            return result;
        }
        finally
        {
            if (shouldWait)
                _initializeSemaphore.Release();
        }
    }
    
    public async Task<Error?> ResetToDefault()
    {
        var result = await InitializeIfNecessary();
        return result == null
            ? _dwmInteraction.ResetToDefault()
            : result;
    }

    public async Task<Error?> ToggleRoundedCorners()
    {
        var result = await InitializeIfNecessary();
        return result == null
            ? _dwmInteraction.ToggleRoundedCorners()
            : result;
    }

    public async Task<Error?> SetSharpCorners()
    {
        var result = await InitializeIfNecessary();
        return result == null 
            ? _dwmInteraction.SetSharpCorners() 
            : result;
    }

    public Task<Error?> SetRoundedCorners() => ResetToDefault();
    public async Task<Error?> RestartDWM()
    {
        var result = DWMInteraction.GetDWMProcess(logger, out _, out var proc);
        if (result != null)
            return result;

        proc.Kill();
        
        await proc.WaitForExitAsync();
        await Task.Delay(TimeSpan.FromSeconds(1)); // Wait for it to start up
        return await ClearCache();
    }

    public async Task<Error?> ClearCache()
    {
        await _initializeSemaphore.WaitAsync();
        try
        {
            _cachedInitializeResult = null;
            _isInitialized = false;
            
            if (DWMInteraction.CachePath.Exists)
                DWMInteraction.CachePath.Delete(true);

            // Commented this out as a clear cache method shouldn't refresh the cache
            // return await Task.Run(async () => await Internal_InitializeIfNecessary(false));
            return null;
        }
        catch (Exception e)
        {
            return e;
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