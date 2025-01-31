// ReSharper disable ArrangeRedundantParentheses
// Struct Location:
// ./System32/uDWM.dll
// Orig Proto: https://github.com/oberrich/win11-toggle-rounded-corners/blob/master/main.cpp#L198
namespace Dawn.Libs.ToggleRoundedCorners.Native.PartialStructs;

/// <remarks>
/// <para/> The file can be located in ./System32/uDWM.dll
/// <para/> The size of the struct can be found in "CDesktopManager::Create" when it interacts with WPF::g_pProcessHeap"
/// <para/> The last size I recorded it was "0x330" (816) bytes
/// <para/> We don't explicitly state the size in the StructLayout as this is more likely to change than the first 30 bytes
/// <para/> If they remove something we might hit an AV exception, so rather not..
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct CDesktopManager
{
    private CDesktopManagerInlinedPointers _0;
    private fixed byte _1[2];

    // [FieldOffset(0x1A)]
    public bool EnableRoundedShadow; // Default value is "false"
    
    // First used in "CDesktopManager::CreateMonitorRenderTargets"
    // [FieldOffset(0x1B)]
    public bool EnableSharpCorners; // Default value is "false"
    
    // [FieldOffset(0x1C)]
    public bool EnableRoundedCorners; // Default value is "false"
}