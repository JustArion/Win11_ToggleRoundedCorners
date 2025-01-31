// https://github.com/ergrelet/windiff/blob/415bf03c7d4c6db24d6d1abaf74ba9f5453d6196/docs/binary_download.md
// #define DRY_RUN
#define DONT_WRITE
#pragma warning disable CA1869
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Lab25;
using Vanara.PInvoke;

if (!IsAdmin())
{
    Console.WriteLine("Please run as administrator");
    return;
}

var searchPathBuilder = new StringBuilder();
// ReSharper disable once PassStringInterpolation
searchPathBuilder.AppendFormat("cache*{0};", string.Empty);
searchPathBuilder.AppendFormat("srv*{0}", "https://msdl.microsoft.com/download/symbols");

var searchPath = searchPathBuilder.ToString();

var imagePath = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "uDWM.dll"));
var proc = GetCurrentProcess();

Console.WriteLine($"[*] Search Path: {searchPath}");

// Critical Setup!
// dbghelp relies on symsrv. The one in the system folder doesn't have symsrv and we shouldn't move a dll like that into there, but instead copy one to us
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

SafeHPROCESS rwHandle = null!;
try
{
    unsafe
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


        SYMBOL_INFO? g_pdmInstanceField = null;
        ThrowLastErrorIfFalse(SymEnumSymbolsEx(proc, baseAddress, "g_pdmInstance", EnumSymbolsCallback, 0, SYMENUM.SYMENUM_OPTIONS_DEFAULT));
        // ThrowLastErrorIfFalse(SymEnumSymbolsEx(proc, baseAddress, "CDesktopManager::s_pDesktopManagerInstance", EnumSymbolsCallback, 0, SYMENUM.SYMENUM_OPTIONS_DEFAULT));
        bool EnumSymbolsCallback(nint psyminfo, uint symbolsize, nint usercontext)
        {
            var symInfo = Marshal.PtrToStructure<SYMBOL_INFO>(psyminfo);
            g_pdmInstanceField = symInfo;
            Console.WriteLine($"Found symbol {symInfo.Name}");
            return false;
        }

        if (g_pdmInstanceField == null)
        {
            Console.WriteLine("[!] No symbols found");
            return;
        }

        Console.WriteLine($"Address of {nameof(g_pdmInstanceField)} : {g_pdmInstanceField.Value.Address}, 0x{g_pdmInstanceField.Value.Address:X}");

        #if DRY_RUN 
        return;
        #endif
        rwHandle = OpenProcess(new (ProcessAccess.PROCESS_ALL_ACCESS), false, pid);
        
        
        // So, there's a global field called g_pdmInstance holding a reference to the CDesktopManager
        // Firstly we get the pointer to the field
        // Then we read from the field where the actual reference lives
        // then we can dereference it.
        // Imagine it as a CDesktopManager**

        var g_pdmInstancePtrAddress = (CDesktopManager**)g_pdmInstanceField.Value.Address;

        var pdmInstanceAddress = ReadAs<nint>(g_pdmInstancePtrAddress);
        Console.WriteLine($"[*] class CDesktopManager* g_pdmInstance: 0x{pdmInstanceAddress:X}");

        var copied_pdmInstance = ReadAs<CDesktopManager>(pdmInstanceAddress.ToPointer());

        Console.WriteLine(JsonSerializer.Serialize(copied_pdmInstance, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true}));

        var g_pdmInstance = (CDesktopManager*)pdmInstanceAddress;

        var offset = GetRelativeOffset(ref copied_pdmInstance, ref copied_pdmInstance.EnableSharpCorners);
        
        var enabled = !copied_pdmInstance.EnableSharpCorners;
        #if !DONT_WRITE
        WriteAs(nint.Add((nint)g_pdmInstance, offset).ToPointer(), ref enabled);
        #endif
    }
}
finally
{
    Console.WriteLine("[*] Cleaning Up!");
    SymCleanup(proc);
    rwHandle?.Dispose();
}



return;
bool IsAdmin() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
unsafe T ReadAs<T>(void* address) where T : unmanaged
{
    var size = Marshal.SizeOf<T>();
    var value = NativeMemory.Alloc((nuint)size); // We can't just do default(T) and send that over. a 0xc0000409 would occur outside the IDE.
    try
    {
        var result = AVGuard.AgainstReads<T>(address, rwHandle);
        if (!result.Success)
            throw result.Exception!;
        
        ThrowLastErrorIfFalse(ReadProcessMemory(rwHandle, (nint)address, (nint)value, size, out _));

        var retVal = *(T*)value;
        Console.WriteLine($"[*] Reading [0x{(nint)address:X}]({size}) as {typeof(T).Name} ({retVal})");
    
        return retVal;
    }
    finally
    {
        NativeMemory.Free(value);
    }
}

unsafe void WriteAs<T>(void* address, ref T value) where T : unmanaged
{
    var result = AVGuard.AgainstWrites<T>(address, rwHandle);
    if (!result.Success)
        throw result.Exception!;

    var size = Marshal.SizeOf<T>();
    Console.WriteLine($"[*] Writing [0x{(nint)address:X}]({size}) as {typeof(T).Name} ({value})");
    ThrowLastErrorIfFalse(WriteProcessMemory(rwHandle, (nint)address, (nint)Unsafe.AsPointer(ref value), size, out _));
}

unsafe int GetRelativeOffset<T, T1>(ref T target, ref T1 targetType) where T : unmanaged where T1 : unmanaged
{
    var targetAddress = (nint)Unsafe.AsPointer(ref target);
    var targetTypeAddress = (nint)Unsafe.AsPointer(ref targetType);
    return (int)(targetTypeAddress - targetAddress);
}

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x330)]
internal struct CDesktopManager
{
    // private nint _0; 
    // private nint _1;
    // private nint _2;
    // private fixed byte _3[2];

    // private fixed byte _[0x1A];
    
    [FieldOffset(0x1A)]
    public bool RoundedShadowEnabled;
    // First used in  CDesktopManager::CreateMonitorRenderTargets
    [FieldOffset(0x1B)]
    public bool EnableSharpCorners;
    [FieldOffset(0x1C)]
    public bool EnableRoundedCorners;
}

