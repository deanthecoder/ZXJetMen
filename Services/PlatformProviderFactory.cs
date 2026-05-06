// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace ZXJetMen.Services;

/// <summary>
/// Creates the platform provider that best matches the current operating system.
/// </summary>
/// <remarks>
/// Windows can use real desktop windows as platforms, while other systems currently need generated ledges to keep the game playable.
/// </remarks>
public static class PlatformProviderFactory
{
    public static IPlatformProvider Create()
    {
        return OperatingSystem.IsWindows()
            ? new WindowsPlatformProvider()
            : new SyntheticPlatformProvider();
    }
}
