// ReSharper disable ArrangeRedundantParentheses
namespace Dawn.Libs.ToggleRoundedCorners.Native.PartialStructs;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct CDesktopManager26H2 : IPartialStruct
{
    // 2 Because of the original CDesktopManager struct above having 2 fixed bytes
    // 2 Because we skip the new Unknown bool and the IsHighContrastMode bool
    public static int Offset => sizeof(CDesktopManagerInlinedPointers) + (sizeof(byte) * 2) + 2;
    public int GetOffset() => Offset;

    // First used in "CDesktopManager::UpdateColorizationColor"
    // public bool Unknown; // Default value is "true"
    
    // First used in "CDesktopManager::IsHighContrastMode"
    // public bool IsHighContrastMode; // Default value is "false"
    
    // First used in "CDesktopManager::GetEffectiveCornerStyle" (CORNER_STYLE 1)
    public bool UseSharpCorners; // Default value is "true"
}