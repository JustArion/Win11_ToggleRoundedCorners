﻿// Big thanks to https://github.com/Rckov/
namespace Dawn.Apps.ToggleRoundedCorners.Tools.Window;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Microsoft.Xaml.Behaviors;

internal sealed partial class BlurBehavior : Behavior<Window>
{
    [LibraryImport("user32.dll")]
    private static partial int SetWindowCompositionAttribute(nint windowHandle, ref WindowCompositionAttributeData data);
    
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += AssociatedObject_Loaded;
        AssociatedObject.StateChanged += AssociatedObject_StateChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= AssociatedObject_Loaded;
        AssociatedObject.StateChanged -= AssociatedObject_StateChanged;
        base.OnDetaching();
    }

    private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        if (AssociatedObject is { } window) 
            EnableBlur(window);
    }

    private void AssociatedObject_StateChanged(object? sender, EventArgs e)
    {
        var screen = Screen.FromHandle(new WindowInteropHelper(AssociatedObject).Handle);
        var workingArea = screen.WorkingArea;

        AssociatedObject.MaxWidth = workingArea.Width + 8;
        AssociatedObject.MaxHeight = workingArea.Height + 8;
    }

    private static void EnableBlur(Window window)
    {
        var windowHelper = new WindowInteropHelper(window);

        var accent = new AccentPolicy
        {
            AccentState = AccentState.AccentEnableAcrylicblurbehind,
            GradientColor = ((uint)0x99 << 24) | (0x99000000 & 0xFFFFFF)
        };

        var accentStructSize = Marshal.SizeOf(accent);

        var accentPtr = Marshal.AllocHGlobal(accentStructSize);
        Marshal.StructureToPtr(accent, accentPtr, false);

        var data = new WindowCompositionAttributeData
        {
            Attribute = WindowCompositionAttribute.WcaAccentPolicy,
            SizeOfData = accentStructSize,
            Data = accentPtr
        };

        _ = SetWindowCompositionAttribute(windowHelper.Handle, ref data);

        Marshal.FreeHGlobal(accentPtr);
    }
}
internal enum AccentState
{
    AccentEnableAcrylicblurbehind = 4
}

[StructLayout(LayoutKind.Sequential)]
internal struct AccentPolicy
{
    public AccentState AccentState;
    public uint AccentFlags;
    public uint GradientColor;
    public uint AnimationId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowCompositionAttributeData
{
    public WindowCompositionAttribute Attribute;
    public IntPtr Data;
    public int SizeOfData;
}

internal enum WindowCompositionAttribute
{
    WcaAccentPolicy = 19
}