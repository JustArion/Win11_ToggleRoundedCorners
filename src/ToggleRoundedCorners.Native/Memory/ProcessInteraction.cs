namespace Dawn.Libs.ToggleRoundedCorners.Native.Memory;

using Serilog;

internal sealed unsafe class ProcessInteraction(uint processId, ILogger logger) : IDisposable
{
    internal readonly SafeHPROCESS _processHandle = OpenProcess(new(ProcessAccess.PROCESS_ALL_ACCESS), false, processId);

    internal SuccessResult<T> RemoteDereference<T>(T* address) where T : unmanaged => Read<T>(address);
    internal SuccessResult<T> RemoteDereference<T>(nint address) where T : unmanaged => Read<T>(address.ToPointer());
    
    internal SuccessResult<T> Read<T>(nint address) where T : unmanaged => Read<T>(address.ToPointer());
    internal SuccessResult<T> Read<T>(void* address) where T : unmanaged
    {
        try
        {
            var tSize = Marshal.SizeOf<T>();
            var value = NativeMemory.Alloc((nuint)tSize);

            try
            {
                var result = AVGuard.AgainstReads<T>(address, _processHandle);
                if (!result.Success)
                    return SuccessResult<T>.Failed with { Exception = result.Exception };

                ThrowLastErrorIfFalse(ReadProcessMemory(_processHandle, (nint)address, (nint)value, tSize, out _));

                var retVal = *(T*)value;

                logger.Verbose("Reading (0x{Address:X})[{Bytes}] as {TypeName} ({ReturnValue})", (nint)address, tSize, typeof(T).Name, retVal);
                return retVal;
            }
            finally
            {
                NativeMemory.Free(value);
            }
        }
        catch (Exception e)
        {
            return SuccessResult<T>.Failed with { Exception = e };
        }
    }

    internal SuccessResult Write<T>(nint address, T value) where T : unmanaged => Write(address.ToPointer(), value);
    internal SuccessResult Write<T>(void* address, T value) where T : unmanaged
    {
        try
        {
            var result = AVGuard.AgainstWrites<T>(address, _processHandle);
            if (!result.Success)
                return result;

            var tSize = sizeof(T);
            // var tSize = Marshal.SizeOf<T>();
            
            logger.Verbose("Writing (0x{Address:X})[{Bytes}] as {TypeName} ({Value})", (nint)address, tSize, typeof(T).Name, value);
            ThrowLastErrorIfFalse(WriteProcessMemory(_processHandle, (nint)address,(nint)Unsafe.AsPointer(ref value), tSize, out _));

            return true;
        }
        catch (Exception e)
        {
            return SuccessResult.Failed with { Exception = e };
        }

    }
    
    internal static int GetRelativeOffset<T, T1>(ref T target, ref T1 targetType) where T : unmanaged where T1 : unmanaged
    {
        var targetAddress = (nint)Unsafe.AsPointer(ref target);
        var targetTypeAddress = (nint)Unsafe.AsPointer(ref targetType);
        return (int)(targetTypeAddress - targetAddress);
    }

    public void Dispose()
    {
        _processHandle.Dispose();
        GC.SuppressFinalize(this);
    }
    
    ~ProcessInteraction() => _processHandle.Dispose();
}