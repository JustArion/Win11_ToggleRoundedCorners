using Dawn.Common.Windows.Extensions;

namespace Dawn.Libs.ToggleRoundedCorners.Native.Memory;

/// <summary>
///  AVGuard is a static class that provides methods to ensure memory safety
/// </summary>
internal static class AVGuard
{
    public static Error? AgainstReads<T>(void* ptr, HPROCESS proc = default) where T : unmanaged
    {
        var result = QueryAddressInformation((nint)ptr, proc);
        if (result is Error error)
            return error;

        var mbi = result.Unwrap();

        var state = mbi.State;
        if (state != MEM_ALLOCATION_TYPE.MEM_COMMIT)
            return new AccessViolationException($"The memory (0x{(nint)ptr:X}) is uncommitted ({state})");

        var end = mbi.BaseAddress + mbi.RegionSize;

        if ((nint)ptr + sizeof(T) > end)
            return new IndexOutOfRangeException($"The pointer (0x{(nint)ptr:X}) is inbetween memory pages, which is unsupported");

        var canRead = (mbi.Protect & Page_Read_Flags) != 0;
        return canRead
            ? default(Error)
            : new AccessViolationException($"May not read memory at 0x{(nint)ptr:X} for type {typeof(T).Name}");
    }
    
    public static Error? AgainstWrites<T>(void* ptr, HPROCESS proc = default) where T : unmanaged
    {
        var result = QueryAddressInformation((nint)ptr, proc);
        if (result is Error error)
            return error;

        var mbi = result.Unwrap();

        if (mbi.State != MEM_ALLOCATION_TYPE.MEM_COMMIT)
            return new AccessViolationException($"The memory (0x{(nint)ptr:X}) is uncommitted ({mbi.State})");

        var end = mbi.BaseAddress + mbi.RegionSize;

        if ((nint)ptr + sizeof(T) > end)
        {
            return new IndexOutOfRangeException($"The pointer (0x{(nint)ptr:X}) is inbetween memory pages, which is unsupported");
        }

        var canWrite = (mbi.Protect & Page_Write_Flags) != 0;
        return canWrite
            ? default(Error)
            : new AccessViolationException($"May not write memory at 0x{(nint)ptr:X} for type {typeof(T).Name}");
    }
    
    public static (MEM_PROTECTION Protect, MEM_ALLOCATION_TYPE State) QueryAddress(nint address)
    {
        if (VirtualQuery(address, out var mi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
            throw GetLastError().GetException()!;
        
        var state = mi.State;
        var protect = mi.Protect;

        return (protect, state);
    }
    public static Result<MEMORY_BASIC_INFORMATION> QueryAddressInformation(nint address, HPROCESS proc)
    {
        if (proc == default)
            proc = GetCurrentProcess();

        if (VirtualQueryEx(proc, address, out var mi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
            return Result.FromLastError();
        
        return mi;
    }

    public const MEM_PROTECTION Page_Execute_Flags = MEM_PROTECTION.PAGE_EXECUTE |
                                                      MEM_PROTECTION.PAGE_EXECUTE_READ |
                                                      MEM_PROTECTION.PAGE_EXECUTE_READWRITE |
                                                      MEM_PROTECTION.PAGE_EXECUTE_WRITECOPY;

    public const MEM_PROTECTION Page_Read_Flags = MEM_PROTECTION.PAGE_READONLY |
                                                  MEM_PROTECTION.PAGE_READWRITE |
                                                  MEM_PROTECTION.PAGE_EXECUTE_READ |
                                                  MEM_PROTECTION.PAGE_EXECUTE_READWRITE |
                                                  MEM_PROTECTION.PAGE_EXECUTE_WRITECOPY |
                                                  MEM_PROTECTION.PAGE_WRITECOPY;

    public const MEM_PROTECTION Page_Write_Flags = MEM_PROTECTION.PAGE_READWRITE |
                                                   MEM_PROTECTION.PAGE_EXECUTE_READWRITE |
                                                   MEM_PROTECTION.PAGE_EXECUTE_WRITECOPY;
}