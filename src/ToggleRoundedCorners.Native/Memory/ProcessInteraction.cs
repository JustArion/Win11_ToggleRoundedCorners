using Dawn.Common.Windows.Extensions;

namespace Dawn.Libs.ToggleRoundedCorners.Native.Memory;

using Serilog;

/// <summary>
/// Handles interactions with a remote process memory.
/// </summary>
internal sealed unsafe class ProcessInteraction(uint processId, ILogger logger) : IDisposable
{
    internal readonly SafeHPROCESS _processHandle = OpenProcess(new(ProcessAccess.PROCESS_ALL_ACCESS), false, processId);

    internal Result<T> RemoteDereference<T>(T* address) where T : unmanaged => Read<T>(address);
    internal Result<T> RemoteDereference<T>(nint address) where T : unmanaged => Read<T>(address.ToPointer());
    
    internal Result<T> Read<T>(nint address) where T : unmanaged => Read<T>(address.ToPointer());
    internal Result<T> Read<T>(void* address) where T : unmanaged
    {
        try
        {
            var tSize = Marshal.SizeOf<T>();
            var value = default(T);

            var error = AVGuard.AgainstReads<T>(address, _processHandle);
            if (error != null)
                return error;
            
            var ok = ReadProcessMemory(_processHandle, (nint)address, (nint)Unsafe.AsPointer(ref value), sizeof(T), out var bytesRead);

            if (!ok || bytesRead != sizeof(T))
                return Result.FromLastError();

            logger.Verbose("Reading (0x{Address:X})[{Bytes}] as {TypeName} ({ReturnValue})", (nint)address, tSize, typeof(T).Name, value);
            return value;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    internal Error? Write<T>(nint address, T value) where T : unmanaged => Write(address.ToPointer(), value);
    internal Error? Write<T>(void* address, T value) where T : unmanaged
    {
        try
        {
            var error = AVGuard.AgainstWrites<T>(address, _processHandle);
            if (error != null)
                return error;

            var tSize = sizeof(T);
            // var tSize = Marshal.SizeOf<T>();
            
            logger.Verbose("Writing (0x{Address:X})[{Bytes}] as {TypeName} ({Value})", (nint)address, tSize, typeof(T).Name, value);
            return WriteProcessMemory(_processHandle, (nint)address,(nint)Unsafe.AsPointer(ref value), tSize, out _) 
                ? null 
                : Result.FromLastError();
        }
        catch (Exception e)
        {
            return e;
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