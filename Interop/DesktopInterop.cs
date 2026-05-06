// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;

namespace ZXJetMen.Interop;

/// <summary>
/// Provides cross-platform helpers for native desktop window behavior.
/// </summary>
/// <remarks>
/// The overlay needs platform handles and click-through behavior, but the rest of the app should not know the native API details.
/// </remarks>
internal static class DesktopInterop
{
    public static IntPtr GetHandle(Window window)
    {
        return window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
    }

    public static void MakeClickThrough(Window window)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsInterop.MakeClickThrough(window);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            MacInterop.MakeClickThrough(window);
        }
    }
}
