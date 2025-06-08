namespace Dawn.Libs.ToggleRoundedCorners.Native.Symbols;

using System.Diagnostics.CodeAnalysis;
using Serilog;

/// <summary>
/// Handles fetching symbols for the uDWM.dll from the Microsoft Symbol Server.
/// </summary>
internal sealed class DWMSymbolHandler(ILogger logger)
{
    [SuppressMessage("ReSharper", "SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault")]
    internal async Task<SuccessResult<SYMBOL_INFO>> GetSymbol(string symbolName, ulong targetBaseAddress)
    {
        var ourDbgHelp = FromAppDirectory("dbghelp.dll");
        
        var proc = GetCurrentProcess();
        var searchPath = SearchPath.CreateDefault();
        
        logger.Debug("Search path for uDWM.dll is {SearchPath}", searchPath);

        if (!ourDbgHelp.Exists)
        {
            var systemDbgHelp = FromSystem("dbghelp.dll");
            logger.Information("Copying dbghelp.dll from {Source} to {Destination}", systemDbgHelp.FullName, ourDbgHelp.FullName);
            systemDbgHelp.CopyTo(ourDbgHelp.FullName);
        }
        
        var ourSymSrv = new FileInfo(Path.Combine(AppContext.BaseDirectory, "symsrv.dll"));

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
        RemoveUnusedSymSrv();

        
        logger.Information("Initializing Microsoft Symbol Database");
        try
        {
            if (!SymInitialize(proc, searchPath, false))
                return Failed<SYMBOL_INFO>();
        
            logger.Information("Loading Symbols from Cache or from Microsoft Servers...");
            var dwmBinary = FromSystem("uDWM.dll");
            var baseAddress = 0UL;
            SYMBOL_INFO? symbol = null;
            var informedSymbolName = false;

            const uint MAX_RETRIES = 5;
            const uint DELAY_BETWEEN_RETRIES_SEC = 2;
            for (var i = 0; i < MAX_RETRIES; i++)
            {
                baseAddress = SymLoadModuleEx(proc, HFILE.NULL, dwmBinary.FullName, BaseOfDll: targetBaseAddress);
                    
                LogPDBInfo(proc, baseAddress);

                if (!informedSymbolName)
                {
                    logger.Information("Looking through symbols for {SymbolName}", symbolName);
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
                }

                if (baseAddress == 0)
                {
                    logger.Warning("({Retry}/{MaxRetries}) Failed to fetch symbols from Microsoft Servers, retrying...", i + 1, MAX_RETRIES);
                    await Task.Delay(TimeSpan.FromSeconds(DELAY_BETWEEN_RETRIES_SEC));
                }
                else
                {
                    logger.Information(i != 0 ? "({Retry}/{MaxRetries}) Found it!" : "Found it!", i + 1, MAX_RETRIES);
                    break;
                }
            }
            
            if (baseAddress == 0)
                return Failed<SYMBOL_INFO>(new TimeoutException("Failed to fetch Symbols from Microsoft Servers, ensure you're connected to the internet"));
            
            logger.Debug("Base Address: 0x{BaseAddress:X}", baseAddress);
            
            if (symbol is null)
                return Failed<SYMBOL_INFO>();

            var offset = symbol.Value.Address - baseAddress;

            if (offset == EXPECTED_OFFSET)
                logger.Debug("Found symbol: {Symbol} at expected offset 0x{Offset:X}", symbol.Value.Name, offset);
            else
                logger.Warning("Found symbol: {Symbol} at unexpected offset 0x{Offset:X}. Expected at offset 0x{ExpectedOffset:X}", symbol.Value.Name, offset, EXPECTED_OFFSET);
            return symbol.Value;
        }
        finally
        {
            SymCleanup(proc);
        }
    }

    private void LogPDBInfo(HPROCESS proc, ulong baseAddress)
    {
        var mod = new IMAGEHLP_MODULE64();
        if (SymGetModuleInfoW64(proc, baseAddress, ref mod))
        {
            logger.Debug("PDB Signature: {Signature}", mod.PdbSig70);
            logger.Debug("PDB Size: {Size} bytes", mod.ImageSize);
        }
    }

    // Name             Offset       Section    Kind
    // ----             -------      -------    ----
    // g_pdmInstance    0x11BBD8    .data       Data           
    private const uint EXPECTED_OFFSET = 0x11BBD8;
    
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

    private SuccessResult<T> Failed<T>(Exception? ex = null)
    {
        ex ??= Kernel32.GetLastError().GetException()!;
        logger.Error(ex, "Failed to fetch Symbols from Microsoft Servers");
        return SuccessResult<T>.Failed with { Exception = ex };
    }

    private static FileInfo FromSystem(string fileName) => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), fileName));
    private static FileInfo FromAppDirectory(string fileName) => new(Path.Combine(AppContext.BaseDirectory, fileName));
}