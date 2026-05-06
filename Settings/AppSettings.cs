// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.Settings;

namespace ZXJetMen.Settings;

/// <summary>
/// Persistent user preferences for ZXJetMen.
/// </summary>
/// <remarks>
/// The overlay has no settings window yet, so DTC.Core settings give tray-menu changes somewhere durable to live.
/// </remarks>
public sealed class AppSettings : UserSettingsBase
{
    public const int DefaultJetmanCount = 3;
    public const int MinJetmanCount = 1;
    public const int MaxJetmanCount = 12;

    public static AppSettings Instance { get; } = new AppSettings();

    protected override string SettingsFileName => "zxjetmen-settings.json";

    public int JetmanCount
    {
        get => Get<int>();
        set => Set(Math.Clamp(value, MinJetmanCount, MaxJetmanCount));
    }

    protected override void ApplyDefaults()
    {
        JetmanCount = DefaultJetmanCount;
    }
}
