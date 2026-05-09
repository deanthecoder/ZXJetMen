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
/// Reads visible macOS desktop windows as playable platforms.
/// </summary>
/// <remarks>
/// CoreGraphics window enumeration is isolated here so the playfield can treat missing window geometry as an empty desktop.
/// </remarks>
public sealed class MacPlatformProvider : IPlatformProvider
{
    public bool ShowSyntheticPlatforms => false;

    public bool IsFrontmostWindowFullscreen { get; private set; }

    public IReadOnlyList<Platform> GetPlatforms(PixelRect screenBounds, double screenScale, IntPtr self)
    {
        var platforms = MacInterop.GetPlatforms(screenBounds, screenScale, out var isFrontmostWindowFullscreen);
        IsFrontmostWindowFullscreen = isFrontmostWindowFullscreen;
        return platforms;
    }
}
