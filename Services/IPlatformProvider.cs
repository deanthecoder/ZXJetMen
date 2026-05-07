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
/// Supplies the platforms currently available to the playfield.
/// </summary>
/// <remarks>
/// Platform discovery differs by operating system, so this interface keeps native window probing and synthetic fallback layouts behind one contract.
/// </remarks>
public interface IPlatformProvider
{
    bool ShowSyntheticPlatforms { get; }

    IReadOnlyList<Platform> GetPlatforms(PixelRect screenBounds, double screenScale, IntPtr self);
}
