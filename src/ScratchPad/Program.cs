using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;
// ReSharper disable SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
var searchPathBuilder = new StringBuilder();
// ReSharper disable once PassStringInterpolation
searchPathBuilder.AppendFormat("cache*{0};", string.Empty);
searchPathBuilder.AppendFormat("srv*{0}", "https://msdl.microsoft.com/download/symbols");

var searchPath = searchPathBuilder.ToString();

var imagePath = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "uDWM.dll"));
var proc = GetCurrentProcess();

Console.WriteLine($"[*] Search Path: {searchPath}");

var ourDbgHelp = new FileInfo(Path.Combine(AppContext.BaseDirectory, "dbghelp.dll"));

if (!ourDbgHelp.Exists)
{
    var systemDbgHelp = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dbghelp.dll"));

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
    
    File.Copy(Path.Combine(AppContext.BaseDirectory, folder, "symsrv.dll"), Path.Combine(AppContext.BaseDirectory, "symsrv.dll"));
}
// ------
try
{
    ThrowLastErrorIfFalse(SymInitialize(proc, searchPath, false));
        
    var hwnd = FindWindow("dwm");
    GetWindowThreadProcessId(hwnd, out var pid);
    Console.WriteLine($"[*] DWM Id: {pid}");

    var managedHandle = Process.GetProcessById((int)pid);
    var uDWM = managedHandle.Modules.Cast<ProcessModule>().ToList().First(x => x.ModuleName == "udwm.dll");

    // Networked
    var baseAddress = SymLoadModuleEx(proc, HFILE.NULL, imagePath.FullName, BaseOfDll: (ulong)uDWM.BaseAddress);

    Console.WriteLine($"[*] Base Address: 0x{baseAddress:X}");

    var mod = new IMAGEHLP_MODULE64();
    mod.SizeOfStruct = (uint)Marshal.SizeOf(mod);


    ThrowLastErrorIfFalse(SymGetModuleInfoW64(proc, baseAddress, ref mod));
    Console.WriteLine($"[*] PDB Signature: {mod.PdbSig70}");
    Console.WriteLine(mod.TimeDateStamp);
    Console.WriteLine($"[*] Size: {mod.ImageSize}");




    var defaultSym = new IMAGEHLP_GET_TYPE_INFO_PARAMS();
    defaultSym.SizeOfStruct = (uint)Marshal.SizeOf(defaultSym);
    var symbols = new List<SYMBOL_INFO>();
    ThrowLastErrorIfFalse(SymEnumSymbolsEx(proc, baseAddress, "*CDesktopManager*", EnumSymbolsCallback, 0, SYMENUM.SYMENUM_OPTIONS_DEFAULT));
    bool EnumSymbolsCallback(nint psyminfo, uint symbolsize, nint usercontext)
    {
        var symInfo = Marshal.PtrToStructure<SYMBOL_INFO>(psyminfo);
        Console.WriteLine($"[*] Found {symInfo.Name}");

        return true;
    }

    Console.WriteLine();

}
finally
{
    Console.WriteLine("[*] Cleaning Up!");
    SymCleanup(proc);
}
