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

    public IReadOnlyList<Platform> GetPlatforms(PixelRect screenBounds, double screenScale, IntPtr self)
    {
        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return [];
        }

        var scale = screenScale > 0 ? screenScale : 1;
        var width = screenBounds.Width / scale;
        var height = screenBounds.Height / scale;
        var platformHeight = Math.Max(80, height * 0.16);

        return
        [
            CreatePlatform(width * 0.06, width * 0.50, height * 0.24, platformHeight, 0),
            CreatePlatform(width * 0.52, width * 0.94, height * 0.34, platformHeight, 1),
            CreatePlatform(width * 0.24, width * 0.72, height * 0.46, platformHeight, 2),
            CreatePlatform(width * 0.10, width * 0.44, height * 0.66, platformHeight, 3),
            CreatePlatform(width * 0.42, width * 0.88, height * 0.74, platformHeight, 4),
            CreatePlatform(width * 0.02, width * 0.98, height * 0.92, Math.Max(64, height * 0.08), 5)
        ];
    }

    private static Platform CreatePlatform(double left, double right, double y, double height, int zOrder)
    {
        return new Platform(left, right, y, y + height, zOrder, true);
    }
}
