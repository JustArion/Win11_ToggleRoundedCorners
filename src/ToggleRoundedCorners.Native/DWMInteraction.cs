using System.Text;
using Dawn.Common.Windows.Extensions;

namespace Dawn.Libs.ToggleRoundedCorners.Native;

using System.Diagnostics;
using System.Text.Json;
using Memory;
using PartialStructs;
using Serilog;
using Symbols;

/// <summary>
/// This class provides an interface to interact with the Desktop Window Manager (DWM) to toggle rounded corners on Windows 11.
/// </summary>
public sealed class DWMInteraction(ILogger logger) : IDisposable
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
    public static DirectoryInfo CachePath => new (Path.Combine(AppContext.BaseDirectory, "sym"));


    public async Task<Error?> Initialize()
    {
        try
        {
            var error = GetDWMProcess(logger, out var pid, out var managedHandle);
            if (error != null)
                return error;
            
            _processModule?.Dispose();
            _processModule = managedHandle.Modules.Cast<ProcessModule>().First(x => x.ModuleName == "udwm.dll");
            
            _processInteraction?.Dispose();
            _processInteraction = new ProcessInteraction(pid, logger);

            var symbolHandler = new DWMSymbolHandler(logger);
            // symbolHandler.AlertProgress += InformProgress;

            var baseAddress = (ulong)_processModule.BaseAddress;
            // We do a lookup for s_pDesktopManagerInstance first since there are more references to it in uDWM.dll than g_pdmInstance
            // They're both the same global reference
            var result = await symbolHandler.GetSymbol("CDesktopManager::s_pDesktopManagerInstance", baseAddress);

            if (result is Error primary)
            {
                logger.Information("Unable to find CDesktopManager::s_pDesktopManagerInstance, doing alternative lookup for g_pdmInstance instead");
                result = await symbolHandler.GetSymbol("g_pdmInstance", baseAddress);
                
                if (result is Error fallback)
                    return primary.Message == fallback.Message
                        ? primary
                        : new Error(primary.Message, new AggregateException(primary.ToException(), fallback.ToException()));
            }

            _symbol = result.Unwrap();
            return null;
        }
        catch (Exception e)
        {
            _processInteraction?.Dispose();
            _processInteraction = null;
            _processModule?.Dispose();
            _processModule = null;
            return e;
        }
    }

    public static Error? GetDWMProcess(ILogger logger, out uint pid, out Process managedHandle)
    {
        managedHandle = null!;
        pid = 0;
        try
        {

            var hwnd = FindWindow("dwm");

            if (hwnd == 0)
            {
                logger.Error("Failed to find DWM window");
                return Result.FromLastError();
            }

            _ = GetWindowThreadProcessId(hwnd, out pid);

            managedHandle = Process.GetProcessById((int)pid);

            return managedHandle.ProcessName == "dwm"
                ? null
                : new Error($"DWM process name is not dwm but instead '{managedHandle.ProcessName}'");
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to find DWM process");
            return e;
        }

    }

    public Error? ResetToDefault() => SetRoundedCorners();
    public Error? ToggleRoundedCorners() => SetRoundedCorners(RoundedCorners.Toggle);
    public Error? SetSharpCorners() => SetRoundedCorners(RoundedCorners.Sharp);
    
    
    // So, there's a global field called g_pdmInstance holding a reference to the CDesktopManager
    // Firstly we get the pointer to the field
    // Then we read from the field where the actual reference lives
    // then we can dereference it.
    // Imagine it as a CDesktopManager**
    private unsafe Error? SetRoundedCorners(RoundedCorners roundedCorners = RoundedCorners.Default)
    {
        if (_processInteraction == null || _processModule == null)
            return "DWM Interaction is not initialized properly";
        
        // Gets CDesktopManager*
        var pCDesktopManager = (CDesktopManager**)_symbol.Address;

        var cDesktopManagerResult = _processInteraction!.Read<nint>(pCDesktopManager);
        if (cDesktopManagerResult.Value is not nint globalFieldValue)
            return (Error)cDesktopManagerResult.Value!;
        
        // Gets a CDesktopManager copy
        var derefResult = _processInteraction.RemoteDereference<CDesktopManager>(globalFieldValue);
        if (derefResult.Value is not CDesktopManager cDesktopManager)
            return (Error)cDesktopManagerResult.Value!;
        
        var json = JsonSerializer.Serialize(cDesktopManager, _serializerOptions);
        logger.Debug("Before Write: CDesktopManager: {Json}", json);

        var stateResult = GetRemoteState(globalFieldValue);
        if (stateResult.Value is not bool enabledOnRemote)
            return (Error)cDesktopManagerResult.Value!;

        var enabled = roundedCorners switch
        {
            RoundedCorners.Default => false,
            RoundedCorners.Toggle => !enabledOnRemote,
            RoundedCorners.Sharp => true,
            _ => throw new ArgumentOutOfRangeException(nameof(roundedCorners), roundedCorners, null)
        };
        
        var error = MutateRemoteState(globalFieldValue, enabled);
        if (error != null)
            return error;

        derefResult = _processInteraction.RemoteDereference<CDesktopManager>(globalFieldValue);
        if (derefResult.IsSuccess)
        {
            json = JsonSerializer.Serialize(cDesktopManager, _serializerOptions);
            logger.Debug("After Write: CDesktopManager: {Json}", json);
        }
        else
            logger.Error((Exception)derefResult.Value!, "Failed to read CDesktopManager after write");

        OnCornersChange(roundedCorners, enabled);

        
        return error;
    }

    private void OnCornersChange(RoundedCorners roundedCorners, bool currentlyRounded)
    {
        switch (roundedCorners)
        {
            case RoundedCorners.Default:
                logger.Information("Reset to default Windows 11 rounded corners!");
                break;
            case RoundedCorners.Toggle:
                logger.Information("Toggled rounded corners from ({Current}) -> ({New})", currentlyRounded ? "Rounded" : "Sharp", currentlyRounded ? "Sharp" : "Rounded");
                break;
            case RoundedCorners.Sharp:
                logger.Information("Your windows now have sharp corners! New windows are automatically sharp!");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(roundedCorners), roundedCorners, null);
        }

        if (currentlyRounded)
        {
            // logger.Information("Click on a window to have it refresh");

        }
        
        try
        {
            RedrawWindows(currentlyRounded);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to redraw windows");
        }
    }

    private static List<HWND> GetAllWindows()
    {
        var retVal = new List<HWND>();
        EnumWindows((hwnd, _) =>
        {
            retVal.Add(hwnd);
            return true;
        }, 0);
        
        return retVal;
    }
    
    /// <summary>
    /// This only updates the currently visible windows of their intended state, using the DWMAPI is not a permanent solution, we can't base the entire app off this
    /// since this app would need to run constantly then, it might have issues with apps that have a higher level of permission (like games with Anti-Cheats)
    /// </summary>
    private void RedrawWindows(bool currentlyRounded)
    {
        var hwnds = GetAllWindows();

        logger.Information("Updating current windows");
        
        foreach (var hwnd in hwnds)
        {
            if (!IsWindowVisible(hwnd))
                continue;
            
            // window title
            var title = new StringBuilder(256);
            _ = GetWindowText(hwnd, title, title.Capacity);

            if (title.Length == 0)
                continue;
            
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
                if (!result.Failed)
                    continue;

                logger.Error(result.GetException()!, "Failed to redraw window {Title}", title);

                SetWindowPos(hwnd, 0, 0, 0, 0, 0,
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOZORDER |
                    SetWindowPosFlags.SWP_NOACTIVATE | SetWindowPosFlags.SWP_FRAMECHANGED);


            }
            catch (Exception e)
            {
                logger.Error(e, "Exception while redrawing window ({Title})", title);
            }
        }
    }


    private Dictionary<int, Func<nint, Result<bool>>>? _versionSpecificGetStates;
    private Result<bool> GetRemoteState(nint pdmInstance)
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

            logger.Debug("Used build specific read for build {Build}", build);
            return function(pdmInstance);
        }

        return new PlatformNotSupportedException("Unable to Read from unsupported Windows version");
    }

    private Result<bool> GetState26(nint g_pdmInstance)
    {
        var result = _processInteraction!.Read<CDesktopManager26H2>(nint.Add(g_pdmInstance, CDesktopManager26H2.Offset));
        if (result is Error error)
            return error;
        
        return new(((CDesktopManager26H2)result.Value).UseSharpCorners);
    }

    private Result<bool> GetState22(nint g_pdmInstance)
    {
        var result = _processInteraction!.Read<CDesktopManager22H2>(nint.Add(g_pdmInstance, CDesktopManager22H2.Offset));
        if (result is Error error)
            return error;
        
        return new(((CDesktopManager22H2)result.Value).UseSharpCorners);
    }

    private Dictionary<int, Func<nint, bool, Error?>>? _versionSpecificMutations;
    private Error? MutateRemoteState(nint g_pdmInstance, bool sharpCorners)
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
            
            logger.Debug("Used build specific mutation for build {Build}", build);
            return function(g_pdmInstance, sharpCorners);
        }

        return new PlatformNotSupportedException("Unable to Write to unsupported Windows version");
    }

    private Error? MutateState26(nint g_pdmInstance, bool sharpCorners)
    {
        var newState = new CDesktopManager26H2
        {
            UseSharpCorners = sharpCorners
        };   
            
        return _processInteraction!.Write(nint.Add(g_pdmInstance, newState.GetOffset()), newState);
    }

    private Error? MutateState22(nint g_pdmInstance, bool sharpCorners)
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