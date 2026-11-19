using System.Diagnostics;

#pragma warning disable CA2254
namespace Dawn.Libs.ToggleRoundedCorners.Native.Symbols;

using System.Diagnostics.CodeAnalysis;
using Serilog;

/// <summary>
/// Handles fetching symbols for the uDWM.dll from the Microsoft Symbol Server.
/// </summary>
[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
internal sealed class DWMSymbolHandler
{
    private readonly ILogger _logger;

    [SuppressMessage("ReSharper", "SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault")]
    public DWMSymbolHandler(ILogger logger)
    {
        _logger = logger;
        
        var ourDbgHelp = FromAppDirectory("dbghelp.dll");
        
        if (!ourDbgHelp.Exists)
        {
            var systemDbgHelp = FromSystem("dbghelp.dll");
            logger.Information("Copying dbghelp.dll from {Source} to {Destination}", systemDbgHelp.FullName, ourDbgHelp.FullName);
            systemDbgHelp.CopyTo(ourDbgHelp.FullName);
        }

        // It's important that we load these from the same directory. It's undocumented behaviour but dbghelp.dll will fail to resolve remote symbols if symsrv.dll isn't in the same dir
        if (!NativeLibrary.TryLoad(ourDbgHelp.FullName, out _))
        {
            logger.Warning("Could not load dbghelp.dll!");
            return;
        }
        
        var ourSymSrv = new FileInfo(Path.Combine(AppContext.BaseDirectory, "symsrv.dll"));
        //
        if (!ourSymSrv.Exists)
        {
            var folder = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "amd64",
                Architecture.X86 => "x86",
                _ => throw new PlatformNotSupportedException()
            };
        
            logger.Information("Copying symsrv.dll from {Source} to {Destination}", Path.Combine(AppContext.BaseDirectory, folder, "symsrv.dll"), ourSymSrv.FullName);
            File.Copy(Path.Combine(AppContext.BaseDirectory, folder, "symsrv.dll"), Path.Combine(AppContext.BaseDirectory, "symsrv.dll"));
        }
        // It's important that we load these from the same directory. It's undocumented behaviour but dbghelp.dll will fail to resolve remote symbols if symsrv.dll isn't in the same dir
        if (!NativeLibrary.TryLoad(ourSymSrv.FullName, out _))
        {
            logger.Warning("Could not load symsrv.dll!");
            return;
        }
        
        RemoveUnusedSymSrv();
    }

    internal async Task<Result<SYMBOL_INFO>> GetSymbol(string symbolName, ulong targetBaseAddress)
    {

        var cache = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "cache"));
        cache.Create();
        var proc = GetCurrentProcess();
        var searchPath = SearchPath.CreateDefault(cache);
        
        _logger.Debug("Search path for uDWM.dll is {SearchPath}", searchPath);

        
        _logger.Information("Initializing Microsoft Symbol Database");
        try
        {
            SymSetOptions(SymGetOptions() | SYMOPT.SYMOPT_DEBUG);

            if (!SymInitialize(proc, searchPath, false))
                return Failed<SYMBOL_INFO>(GetLastError().GetException()!);
            
            // PSYMBOL_REGISTERED_CALLBACK64 symCallback = (process, action, data, context) =>
            // {
            //     switch (action)
            //     {
            //         case CBA.CBA_EVENT:
            //             // Symbol server messages (download start, HTTP errors, "not found") arrive here.
            //             var ev = Marshal.PtrToStructure<IMAGEHLP_CBA_EVENT>((nint)data);
            //             Console.WriteLine($"SymEvent sev={ev.severity}: {ev.desc}");
            //             break;
            //         case CBA.CBA_DEBUG_INFO:
            //             // DbgHelp debug output, enabled by SYMOPT_DEBUG.
            //             Console.WriteLine($"DbgHelp: {Marshal.PtrToStringUni((nint)data)}");
            //             break;
            //     }
            //     return false;
            // };
            //
            // if (!SymRegisterCallbackW64(proc, symCallback, 0))
            //     throw GetLastError().GetException()!;
        
            _logger.Information("Loading Symbols from Cache or from Microsoft Servers...");
            var dwmBinary = FromSystem("uDWM.dll");
            var baseAddress = 0UL;
            SYMBOL_INFO? symbol = null;
            var informedSymbolName = false;

            const uint MAX_RETRIES = 5;
            const uint DELAY_BETWEEN_RETRIES_SEC = 2;
            for (var i = 0; i < MAX_RETRIES; i++)
            {
                baseAddress = SymLoadModuleEx(proc, HFILE.NULL, dwmBinary.FullName, "uDWM", BaseOfDll: targetBaseAddress);
                if (baseAddress == 0)
                {
                    _logger.Error(GetLastError().GetException(), "Loading modules failed");
                    continue;
                }
                    
                LogPDBInfo(proc, baseAddress);
                
                // using (var sym = new SafeIMAGEHLP_SYMBOL64())
                // {
                //     if (SymGetSymFromName64(proc, "uDWM!g_pdmInstance", sym)) 
                //         _logger.Debug("{Name} - {Addr:X}", sym.Value.Name, sym.Value.Address);
                //     else
                //         _logger.Debug(GetLastError().GetException(), "Failed to get sym");
                //
                //     Process.GetCurrentProcess().Kill();
                // }

                if (!informedSymbolName)
                {
                    _logger.Information("Looking through symbols for {SymbolName}", symbolName);
                    informedSymbolName = true;
                }
                    
                if (!SymEnumSymbolsEx(proc, baseAddress, symbolName, (info, _, _) =>
                    {
                        var symInfo = Marshal.PtrToStructure<SYMBOL_INFO>(info);
                        symbol = symInfo;
                        return false;
                    }, 0, SYMENUM.SYMENUM_OPTIONS_DEFAULT))
                    return Failed<SYMBOL_INFO>();

                if (symbol == null)
                {
                    SymUnloadModule64(proc, baseAddress);
                    baseAddress = 0;
                    Log.Debug("Symbol fetch failed for {SymbolName}", symbolName);
                }

                if (baseAddress == 0)
                {
                    _logger.Warning("({Retry}/{MaxRetries}) Failed to fetch symbols from Microsoft Servers, retrying...", i + 1, MAX_RETRIES);
                    await Task.Delay(TimeSpan.FromSeconds(DELAY_BETWEEN_RETRIES_SEC));
                }
                else
                {
                    _logger.Information(i != 0 ? "({Retry}/{MaxRetries}) Found it!" : "Found it!", i + 1, MAX_RETRIES);
                    break;
                }
            }
            
            if (baseAddress == 0)
                return Failed<SYMBOL_INFO>(new TimeoutException("Ensure you're connected to the internet\nAlternatively: Microsoft hasn't published the latest Symbols for this Windows version"));
            
            _logger.Debug("Base Address: 0x{BaseAddress:X}", baseAddress);
            
            if (symbol is null)
                return Failed<SYMBOL_INFO>();

            var offset = symbol.Value.Address - baseAddress;

            _logger.Debug("Found symbol: {Symbol} at offset 0x{Offset:X}", symbol.Value.Name, offset);
            return symbol.Value;
        }
        finally
        {
            SymCleanup(proc);
        }
    }

    private void LogPDBInfo(HPROCESS proc, ulong baseAddress)
    {
        var mod = new IMAGEHLP_MODULE64 { SizeOfStruct = (uint)Marshal.SizeOf<IMAGEHLP_MODULE64>() };
        if (SymGetModuleInfoW64(proc, baseAddress, ref mod))
        {
            _logger.Debug("SymType: {SymType}", mod.SymType);
            _logger.Debug("Loaded PDB: {Pdb}", mod.LoadedPdbName);
            _logger.Debug("PDB GUID: {Guid}", mod.PdbSig70);
            _logger.Debug("PDB Age: {Age}", mod.PdbAge);
        }
        else
            _logger.Debug(GetLastError().GetException(), "Couldn't get PDB info on base address {Address:X}",
                baseAddress);
    }
    
    private static void RemoveUnusedSymSrv()
    {
        var x86 = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "x86"));
        if (x86.Exists)
            x86.Delete(true);
        
        var x64 = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "amd64"));
        if (x64.Exists)
            x64.Delete(true);
        
        var arm64 = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "arm64"));
        if (arm64.Exists)
            arm64.Delete(true);
    }

    private Result<T> Failed<T>(Exception? ex = null)
    {
        ex ??= GetLastError().GetException()!;
        _logger.Error(ex, "Failed to fetch Symbols from Microsoft Servers!\n-");
        return ex;
    }

    private static FileInfo FromSystem(string fileName) => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), fileName));
    private static FileInfo FromAppDirectory(string fileName) => new(Path.Combine(AppContext.BaseDirectory, fileName));
}