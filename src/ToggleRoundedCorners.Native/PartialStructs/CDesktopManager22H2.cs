// ReSharper disable ArrangeRedundantParentheses
namespace Dawn.Libs.ToggleRoundedCorners.Native.PartialStructs;

/// <summary>
/// Size: 0x330 (816) bytes
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct CDesktopManager22H2 : IPartialStruct
{
    // 2 because of the original CDesktopManager struct above having 2 fixed bytes
    // 1 because we're skipping UseRoundedShadows
    public int GetOffset() => Offset;

    public static int Offset => sizeof(CDesktopManagerInlinedPointers) + (sizeof(byte) * 2) + 1;
    
    // public bool UseRoundedShadows; // Default value is "false"
    
    // First used in "CDesktopManager::CreateMonitorRenderTargets"
    public bool UseSharpCorners; // Default value is "false"
    
    // public bool UseRoundedCorners; // Default value is "false"
}