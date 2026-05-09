// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using ZXJetMen.Interop;
using ZXJetMen.Models;

namespace ZXJetMen.Services;

/// <summary>
/// Reads visible Windows desktop windows as playable platforms.
/// </summary>
/// <remarks>
/// This adapter keeps Win32 and DWM details out of the playfield while preserving the desktop-as-level behavior that makes ZXJetMen fun.
/// </remarks>
public sealed class WindowsPlatformProvider : IPlatformProvider
{
    public bool ShowSyntheticPlatforms => false;

    public bool IsFrontmostWindowFullscreen { get; private set; }

    public IReadOnlyList<Platform> GetPlatforms(PixelRect screenBounds, double screenScale, IntPtr self)
    {
        var platforms = WindowsInterop.GetPlatforms(screenBounds, screenScale, self, out var isFrontmostWindowFullscreen);
        IsFrontmostWindowFullscreen = isFrontmostWindowFullscreen;
        return platforms;
    }
}
