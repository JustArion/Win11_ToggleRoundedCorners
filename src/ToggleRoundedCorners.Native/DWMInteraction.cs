namespace Dawn.Libs.ToggleRoundedCorners.Native;

using System.Diagnostics;
using System.Text.Json;
using Memory;
using PartialStructs;
using Serilog;
using Symbols;

public sealed class DWMInteraction(ILogger logger) : ProgressAwareObject, IDisposable
{
    public enum RoundedCorners
    {
        Default,
        Toggle,
        Sharp
    }

    private ProcessInteraction? _processInteraction;
    private ProcessModule? _processModule;
    private SYMBOL_INFO _symbol;
    public DirectoryInfo CachePath => new (Path.Combine(AppContext.BaseDirectory, "sym"));


    public async Task<SuccessResult> Initialize()
    {
        try
        {
            var result = GetDWMProcess(logger, out var pid, out var managedHandle);
            if (!result)
                return result;
            
            _processModule?.Dispose();
            _processModule = managedHandle.Modules.Cast<ProcessModule>().First(x => x.ModuleName == "udwm.dll");
            
            _processInteraction?.Dispose();
            _processInteraction = new ProcessInteraction(pid, logger);

            var symbolHandler = new DWMSymbolHandler(logger);
            symbolHandler.AlertProgress += InformProgress;

            var baseAddress = (ulong)_processModule.BaseAddress;
            var symbolResult = await symbolHandler.GetSymbol("g_pdmInstance", baseAddress);

            if (!symbolResult.Success)
            {
                var ex1 = symbolResult.Exception!;
                InformProgress("Unable to find g_pdmInstance, doing alternative lookup for CDesktopManager::s_pDesktopManagerInstance instead");
                symbolResult = await symbolHandler.GetSymbol("CDesktopManager::s_pDesktopManagerInstance", baseAddress);
                
                if (!symbolResult.Success)
                {
                    var ex2 = symbolResult.Exception!;
                    
                    if (ex1.Message == ex2.Message)
                        return SuccessResult.Failed with { Exception = ex1 };
                    
                    return SuccessResult.Failed with { Exception = new AggregateException(ex1, ex2) };
                }
            }

            _symbol = symbolResult.Value;
            return true;
        }
        catch (Exception e)
        {
            _processInteraction?.Dispose();
            _processInteraction = null;
            _processModule?.Dispose();
            _processModule = null;
            return SuccessResult.Failed with { Exception = e };
        }
    }

    public static SuccessResult GetDWMProcess(ILogger logger, out uint pid, out Process managedHandle)
    {
        managedHandle = null!;
        pid = 0;
        try
        {

            var hwnd = FindWindow("dwm");

            if (hwnd == 0)
            {
                logger.Error("Failed to find DWM window");
                return SuccessResult.Failed with { Exception = Kernel32.GetLastError().GetException()! };
            }

            _ = GetWindowThreadProcessId(hwnd, out pid);

            managedHandle = Process.GetProcessById((int)pid);
            
            return managedHandle.ProcessName == "dwm" ? true : SuccessResult.Failed with { Exception = new Exception($"DWM process name is not dwm but instead '{managedHandle.ProcessName}'") };
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to find DWM process");
            return SuccessResult.Failed  with { Exception = e };
        }

    }

    public SuccessResult ResetToDefault() => SetRoundedCorners();
    public SuccessResult ToggleRoundedCorners() => SetRoundedCorners(RoundedCorners.Toggle);
    public SuccessResult SetSharpCorners() => SetRoundedCorners(RoundedCorners.Sharp);
    
    
    // So, there's a global field called g_pdmInstance holding a reference to the CDesktopManager
    // Firstly we get the pointer to the field
    // Then we read from the field where the actual reference lives
    // then we can dereference it.
    // Imagine it as a CDesktopManager**
    private unsafe SuccessResult SetRoundedCorners(RoundedCorners roundedCorners = RoundedCorners.Default)
    {
        if (_processInteraction == null || _processModule == null)
            return SuccessResult.Failed with { Exception = new Exception("DWM Interaction is not initialized properly") };
        
        // Gets CDesktopManager*
        var g_pdmInstance = (CDesktopManager**)_symbol.Address;
        
        _processInteraction!.Read<nint>(g_pdmInstance).Deconstruct(out var success, out var exception, out var globalFieldValue);
        if (!success)
            return SuccessResult.Failed with { Exception = exception };
        
        // Gets a CDesktopManager copy
        _processInteraction.RemoteDereference<CDesktopManager>(globalFieldValue).Deconstruct(out success, out exception, out var pdmInstance);
        if (!success)
            return SuccessResult.Failed with { Exception = exception };
        
        var json = JsonSerializer.Serialize(pdmInstance, _serializerOptions);
        logger.Debug("Before Write: CDesktopManager: {Json}", json);

        GetRemoteState(globalFieldValue).Deconstruct(out success, out exception, out var enabledOnRemote);
        if (!success)
            return SuccessResult.Failed with { Exception = exception };

        var enabled = roundedCorners switch
        {
            RoundedCorners.Default => false,
            RoundedCorners.Toggle => !enabledOnRemote,
            RoundedCorners.Sharp => true,
            _ => throw new ArgumentOutOfRangeException(nameof(roundedCorners), roundedCorners, null)
        };
        
        var retVal = MutateRemoteState(globalFieldValue, enabled);

        if (!retVal.Success)
            return retVal;
        
        _processInteraction.RemoteDereference<CDesktopManager>(globalFieldValue).Deconstruct(out success, out exception, out pdmInstance);
        if (success)
        {
            json = JsonSerializer.Serialize(pdmInstance, _serializerOptions);
            logger.Debug("After Write: CDesktopManager: {Json}", json);
        }
        else
            logger.Error(exception, "Failed to read CDesktopManager after write");

        switch (roundedCorners)
        {
            case RoundedCorners.Default:
                InformProgress("Reset to default Windows 11 rounded corners!");
                break;
            case RoundedCorners.Toggle:
                InformProgress($"Toggled rounded corners from ({(enabled ? "Rounded" : "Sharp")}) -> ({(enabled ? "Sharp" : "Rounded")})");
                break;
            case RoundedCorners.Sharp:
                InformProgress("Your windows now have sharp corners! New windows are automatically sharp!");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(roundedCorners), roundedCorners, null);
        }
        
        if (enabled)
            InformProgress("Click on a window to have it refresh");
        
        return retVal;
    }
    
    /// <summary>
    /// This only updates the currently visible windows of their intended state, using the DWMAPI is not a permanent solution, we can't base the entire app off this
    /// since this app would need to run constantly then, it might have issues with apps that have a higher level of permission (like games with Anti-Cheats)
    /// </summary>
    private void RedrawWindows(bool currentlyRounded)
    {
        var hwnds = GetAllWindows();

        foreach (var hwnd in hwnds)
        {
            if (!IsWindowVisible(hwnd))
                continue;
            
            // window title
            var title = new StringBuilder(256);
            _ = GetWindowText(hwnd, title, title.Capacity);

            if (title.Length == 0)
                continue;
            
            logger.Information("Updating current windows");
            logger.Debug("Redrawing window: {Title}", title);
            
            try
            {
                // Redraw the window
                var preference = currentlyRounded
                    ? DwmApi.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND
                    : DwmApi.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                
                // logger.Debug("Preference: {Preference}", preference);

                var result = DwmApi.DwmSetWindowAttribute(hwnd,
                    DwmApi.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, preference);


                SetWindowPos(hwnd, 0, 0, 0, 0, 0,
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER |
                    SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_FRAMECHANGED);

                if (!result.Failed)
                    continue;

                logger.Error(result.GetException()!, "Failed to redraw window {Title}", title);
            }
            catch (Exception e)
            {
                logger.Error(e, "Exception while redrawing window ({Title})", title);
            }
        }
    }


    private Dictionary<int, Func<nint, SuccessResult<bool>>>? _versionSpecificGetStates;
    private SuccessResult<bool> GetRemoteState(nint pdmInstance)
    {
        _versionSpecificGetStates ??= new()
        {
            {26000, GetState26 },
            {22000, GetState22 },
        };
        
        foreach (var (build, function) in _versionSpecificGetStates)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, build))
                continue;

            logger.Information("Used build specific read for build {Build}", build);
            return function(pdmInstance);
        }

        return SuccessResult<bool>.Failed with
        {
            Exception = new PlatformNotSupportedException("Unable to Read from unsupported Windows version")
        };
    }

    private SuccessResult<bool> GetState26(nint g_pdmInstance)
    {
        _processInteraction!.Read<CDesktopManager26H2>(nint.Add(g_pdmInstance, CDesktopManager26H2.Offset)).Deconstruct(out var success, out var exception, out var state);
        if (!success)
            return SuccessResult<bool>.Failed with { Exception = exception };
        
        return state.UseSharpCorners;
    }

    private SuccessResult<bool> GetState22(nint g_pdmInstance)
    {
        _processInteraction!.Read<CDesktopManager22H2>(nint.Add(g_pdmInstance, CDesktopManager22H2.Offset)).Deconstruct(out var success, out var exception, out var state);
        if (!success)
            return SuccessResult<bool>.Failed with { Exception = exception };
        
        return state.UseSharpCorners;
    }

    private Dictionary<int, Func<nint, bool, SuccessResult>>? _versionSpecificMutations;
    private SuccessResult MutateRemoteState(nint g_pdmInstance, bool sharpCorners)
    {
        _versionSpecificMutations ??= new()
        {
            {26000, MutateState26 },
            {22000, MutateState22 },
        };

        foreach (var (build, function) in _versionSpecificMutations)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, build)) 
                continue;
            
            logger.Information("Used build specific mutation for build {Build}", build);
            return function(g_pdmInstance, sharpCorners);
        }

        return SuccessResult.Failed with
        {
            Exception = new PlatformNotSupportedException("Unable to Write to unsupported Windows version")
        };
    }

    private SuccessResult MutateState26(nint g_pdmInstance, bool sharpCorners)
    {
        var newState = new CDesktopManager26H2
        {
            UseSharpCorners = sharpCorners
        };   
            
        return _processInteraction!.Write(nint.Add(g_pdmInstance, newState.GetOffset()), newState);
    }

    private SuccessResult MutateState22(nint g_pdmInstance, bool sharpCorners)
    {
        var newState = new CDesktopManager22H2
        {
            UseSharpCorners = sharpCorners
        };
        
        return _processInteraction!.Write(nint.Add(g_pdmInstance, newState.GetOffset()), newState);

    }

    private static readonly JsonSerializerOptions _serializerOptions =
        new() { WriteIndented = true, IncludeFields = true };

    public void Dispose()
    {
        _processInteraction?.Dispose();
        _processModule?.Dispose();
    }
}