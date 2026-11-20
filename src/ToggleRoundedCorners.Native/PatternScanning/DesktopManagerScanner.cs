using System.Diagnostics;
using Dawn.Common.Windows.Extensions;
using Dawn.Libs.ToggleRoundedCorners.Native.Memory;
using Iced.Intel;
using Serilog;

namespace Dawn.Libs.ToggleRoundedCorners.Native.PatternScanning;

using Reloaded.Memory.Sigscan;

public class DesktopManagerScanner(Process dwm, ProcessModule uDwm, ILogger logger) : IDisposable
{
    private readonly SafeHPROCESS _processHandle = OpenProcess(new(ProcessAccess.PROCESS_ALL_ACCESS), false, (uint)dwm.Id);
    public readonly record struct ScanResult(nint MatchAddress, nint? GlobalAddress);
    
    private static readonly Dictionary<string, string> _patterns = new()
    {
        {"CTopLevelWindow::GetEffectiveCornerStyle", Patterns.GET_EFFECTIVE_CORNER_STYLE_PATTERN},
        {"CWindowList::IsRemoteAppMode", Patterns.IS_REMOTE_APP_MODE_PATTERN},
        {"CDesktopManager::DwmEventThreadProc", Patterns.DWM_EVENT_THREAD_PROC_PATTERN},
        {"CWindowIconic::Initialize", Patterns.INITIALIZE_PATTERN},
        {"CStoryboard::Abandon", Patterns.ABANDON_PATTERN}
    };

    private const int MinAgreement = 3;
    // CDesktopManager**
    public Result<nint> GetDesktopManager()
    {
        var best = ScanAll()
            .Where(x => x.Value.GlobalAddress is not null)
            .GroupBy(x => x.Value.GlobalAddress!.Value)
            .Where(g => g.Count() >= MinAgreement)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (best is null)
            return Result.Fail<nint>($"Fewer than {MinAgreement} patterns agreed on a CDesktopManager address");

        logger.Debug("{Count} patterns agreed on 0x{Address:X}: {Names}",
            best.Count(), best.Key - uDwm.BaseAddress, string.Join(", ", best.Select(x => x.Key)));

        return best.Key;
    }
    
    public Dictionary<string, ScanResult> ScanAll()
    {
        var scanner = new Scanner(dwm, uDwm);
        var results = new Dictionary<string, ScanResult>();

        foreach (var (name, pattern) in _patterns)
        {
            var result = scanner.FindPattern(pattern);
            if (!result.Found)
            {
                logger.Debug("Couldn't find a pattern match for {Name}, it might have been removed", name);
                continue;
            }

            var match = uDwm.BaseAddress + result.Offset;
            var global = ResolveRipGlobal(match);
            results[name] = new ScanResult(match, global);

            logger.Debug("Name: {Name}, Match: {Match:X}, Global: {Global}, Relative: {Relative}", name, match, global is {} ptr 
                ? ptr.ToString("X") 
                : "n/a", 
                global is {} g 
                    ? (g - uDwm.BaseAddress).ToString("X") 
                    : "n/a");
        }

        return results;
    }

    // Returns the referenced address, or null if the instruction isn't that form.
    private nint? ResolveRipGlobal(nint address)
    {
        // x86 instructions are at most 15 bytes, so one read covers any single instruction.
        var bytes = ReadBytes(address, 15);

        var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
        decoder.IP = (ulong)address;
        var instr = decoder.Decode();

        if (instr.IsInvalid)
            return null;

        if (instr.Mnemonic is not (Mnemonic.Mov or Mnemonic.Lea))
            return null;

        if (instr.Op1Kind != OpKind.Memory || instr.MemoryBase != Register.RIP)
            return null;

        // Iced already computes the absolute target, including the next-IP adjustment.
        return (nint)instr.IPRelativeMemoryAddress;
    }
    
    internal unsafe Result<byte[]> ReadBytes(nint address, int count)
    {
        try
        {
            var error = AVGuard.AgainstReads<byte>((void*)address, _processHandle);
            if (error != null)
                return error;

            var buffer = new byte[count];
            fixed (byte* p = buffer)
            {
                if (!ReadProcessMemory(_processHandle, address, (nint)p, count, out var read) || read != count)
                    return Result.FromLastError();
            }

            return buffer;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        GC.SuppressFinalize(this);
        _processHandle.Dispose();
    }
}