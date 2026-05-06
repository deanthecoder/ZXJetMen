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
using ZXJetMen.Models;

namespace ZXJetMen.Services;

/// <summary>
/// Generates faint in-app platforms for systems without native window discovery.
/// </summary>
/// <remarks>
/// This gives macOS and Linux a useful playfield while avoiding fragile or permission-heavy window enumeration during early polish.
/// </remarks>
public sealed class SyntheticPlatformProvider : IPlatformProvider
{
    public bool ShowSyntheticPlatforms => true;

    public IReadOnlyList<Platform> GetPlatforms(PixelRect screenBounds, IntPtr self)
    {
        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return [];
        }

        var width = screenBounds.Width;
        var height = screenBounds.Height;
        var platformHeight = Math.Max(96, height * 0.2);

        return
        [
            CreatePlatform(width * 0.06, width * 0.38, height * 0.30, platformHeight, 0),
            CreatePlatform(width * 0.56, width * 0.94, height * 0.42, platformHeight, 1),
            CreatePlatform(width * 0.18, width * 0.72, height * 0.66, platformHeight, 2),
            CreatePlatform(width * 0.02, width * 0.98, height * 0.92, Math.Max(64, height * 0.08), 3)
        ];
    }

    private static Platform CreatePlatform(double left, double right, double y, double height, int zOrder)
    {
        return new Platform(left, right, y, y + height, zOrder, true);
    }
}
