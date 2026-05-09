// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ZXJetMen.Interop;
using ZXJetMen.Services;
using ZXJetMen.Views;

namespace ZXJetMen;

/// <summary>
/// Hosts the transparent desktop overlay where the game actors are drawn.
/// </summary>
/// <remarks>
/// The window handles screen fitting, click-through setup, and the fixed-rate simulation tick so the playfield can focus on game state.
/// </remarks>
public sealed class OverlayWindow : Window
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 15.0);
    private readonly IPlatformProvider m_platformProvider = PlatformProviderFactory.Create();
    private readonly PlayfieldView m_view;
    private readonly Stopwatch m_clock = Stopwatch.StartNew();
    private readonly Stopwatch m_totalClock = Stopwatch.StartNew();
    private PixelRect m_activeScreenBounds;
    private double m_activeScreenScale = 1;
    private Timer m_timer;
    private int m_tickQueued;

    public OverlayWindow(int jetmanCount, bool miniMode)
    {
        m_view = new PlayfieldView(jetmanCount, miniMode);

        // Transparent, borderless overlay that sits above the primary desktop.
        Content = m_view;
        Background = Brushes.Transparent;
        CanResize = false;
        ExtendClientAreaToDecorationsHint = false;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        Icon = IconLoader.LoadWindowIcon();
        ShowInTaskbar = false;
        SystemDecorations = SystemDecorations.None;
        Title = "ZXJetMen";
        Topmost = true;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowStartupLocation = WindowStartupLocation.Manual;
        m_view.ShowSyntheticPlatforms = m_platformProvider.ShowSyntheticPlatforms;

        Opened += (_, _) =>
        {
            FitVirtualDesktop();
            DesktopInterop.MakeClickThrough(this);
            m_view.Reset();
            m_clock.Restart();

            // Drive the jetman simulation from a background timer, then marshal back to the UI thread.
            m_timer = new Timer(_ =>
            {
                if (Interlocked.Exchange(ref m_tickQueued, 1) == 0)
                {
                    Dispatcher.UIThread.Post(Tick);
                }
            }, null, TimeSpan.Zero, FrameInterval);
        };

        Closed += (_, _) => m_timer?.Dispose();
    }

    private void Tick()
    {
        try
        {
            Interlocked.Exchange(ref m_tickQueued, 0);

            // Clamp large pauses so debugging/breakpoints do not launch jetmen through platforms.
            var dt = Math.Min(m_clock.Elapsed.TotalSeconds, 0.1);
            m_clock.Restart();
            var platforms = m_platformProvider.GetPlatforms(m_activeScreenBounds, m_activeScreenScale, DesktopInterop.GetHandle(this));
            m_view.Step(
                dt,
                m_totalClock.Elapsed.TotalSeconds,
                platforms,
                m_platformProvider.IsFrontmostWindowFullscreen);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private void FitVirtualDesktop()
    {
        // Prototype scope: only use the primary monitor for now.
        var primary = Screens.Primary ?? Screens.ScreenFromWindow(this);
        if (primary is null)
        {
            Width = 1280;
            Height = 720;
            return;
        }

        m_activeScreenBounds = OperatingSystem.IsMacOS()
            ? primary.WorkingArea
            : primary.Bounds;
        m_activeScreenScale = primary.Scaling > 0 ? primary.Scaling : 1;
        var logicalWidth = m_activeScreenBounds.Width / m_activeScreenScale;
        var logicalHeight = m_activeScreenBounds.Height / m_activeScreenScale;
        Position = new PixelPoint(m_activeScreenBounds.X, m_activeScreenBounds.Y);
        Width = logicalWidth;
        Height = logicalHeight;
        m_view.Width = logicalWidth;
        m_view.Height = logicalHeight;
    }

    public void SetJetmanCount(int jetmanCount)
    {
        m_view.SetJetmanLimit(jetmanCount);
    }

    public void SetMiniMode(bool miniMode)
    {
        m_view.SetMiniMode(miniMode);
    }
}
