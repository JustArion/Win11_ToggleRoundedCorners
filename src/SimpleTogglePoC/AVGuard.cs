namespace Lab25;

using Vanara.PInvoke;

internal static class AVGuard
{
    public static unsafe SuccessResult AgainstReads<T>(void* ptr, HPROCESS proc = default) where T : unmanaged
    {
        var addressInfo = QueryAddressInformation((nint)ptr, proc);

        var state = (MEM_ALLOCATION_TYPE)addressInfo.State;
        if (state != MEM_ALLOCATION_TYPE.MEM_COMMIT)
            return SuccessResult.Failed with { Exception = new AccessViolationException($"The memory (0x{(nint)ptr:X} is uncommitted ({state})") };

        var end = addressInfo.BaseAddress + addressInfo.RegionSize;

        if ((nint)ptr + sizeof(T) > end)
        {
            return SuccessResult.Failed with
            {
                Exception = new IndexOutOfRangeException("The pointer is inbetween memory pages, which is unsupported")
            };
        }


        var canRead = (addressInfo.Protect & (uint)Page_Read_Flags) != 0;
        var canWrite = (addressInfo.Protect & (uint)Page_Write_Flags) != 0;

        return canRead 
            ? true 
            : SuccessResult.Failed with { Exception = new AccessViolationException() };
    }
    
    public static unsafe SuccessResult AgainstWrites<T>(void* ptr, HPROCESS proc = default) where T : unmanaged
    {
        var addressInfo = QueryAddressInformation((nint)ptr, proc);

        var state = (MEM_ALLOCATION_TYPE)addressInfo.State;
        if (state != MEM_ALLOCATION_TYPE.MEM_COMMIT)
            return SuccessResult.Failed with { Exception = new AccessViolationException($"The memory is uncommitted ({state})") };

        var end = addressInfo.BaseAddress + addressInfo.RegionSize;

        if ((nint)ptr + sizeof(T) > end)
        {
            return SuccessResult.Failed with
            {
                Exception = new IndexOutOfRangeException("The pointer is inbetween memory pages, which is unsupported")
            };
        }


        var canWrite = (addressInfo.Protect & (uint)Page_Write_Flags) != 0;

        return canWrite 
            ? true 
            : SuccessResult.Failed with { Exception = new AccessViolationException() };
    }
    
    public static unsafe (MEM_PROTECTION Protect, MEM_ALLOCATION_TYPE State) QueryAddress(nint address)
    {
        var arr = new MEMORY_BASIC_INFORMATION[1];
        if (VirtualQuery(address, arr, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
            throw Kernel32.GetLastError().GetException()!;
        
        var mi = arr[0];
        var state = (MEM_ALLOCATION_TYPE)mi.State;
        var protect = (MEM_PROTECTION)mi.Protect;

        return (protect, state);
    }
    public static unsafe MEMORY_BASIC_INFORMATION QueryAddressInformation(nint address, HPROCESS proc)
    {
        if (proc == default)
            proc = GetCurrentProcess();

        var mi = new MEMORY_BASIC_INFORMATION();
        if (VirtualQueryEx(proc, address, (nint)(&mi), sizeof(MEMORY_BASIC_INFORMATION)) == 0)
            throw Kernel32.GetLastError().GetException()!;
        
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