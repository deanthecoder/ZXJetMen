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
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ZXJetMen.Services;
using ZXJetMen.Settings;

namespace ZXJetMen;

/// <summary>
/// Wires the desktop lifetime to the overlay window and tray icon.
/// </summary>
/// <remarks>
/// The application has no normal main window chrome, so this class owns the shutdown path users need from the tray or menu bar.
/// </remarks>
public sealed class App : Application
{
    private readonly AppSettings m_settings = AppSettings.Instance;
    private TrayIcon m_trayIcon;
    private NativeMenuItem m_addJetmanItem;
    private NativeMenuItem m_removeJetmanItem;
    private NativeMenuItem m_jetmanCountItem;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var overlayWindow = new OverlayWindow(m_settings.JetmanCount);
            desktop.MainWindow = overlayWindow;
            m_trayIcon = CreateTrayIcon(desktop, overlayWindow);
            desktop.Exit += (_, _) =>
            {
                m_trayIcon?.Dispose();
                m_settings.Save();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, OverlayWindow overlayWindow)
    {
        m_jetmanCountItem = new NativeMenuItem
        {
            Header = string.Empty,
            IsEnabled = false
        };

        m_addJetmanItem = new NativeMenuItem
        {
            Header = "Add Jetman"
        };
        m_addJetmanItem.Click += (_, _) => SetJetmanCount(overlayWindow, m_settings.JetmanCount + 1);

        m_removeJetmanItem = new NativeMenuItem
        {
            Header = "Remove Jetman"
        };
        m_removeJetmanItem.Click += (_, _) => SetJetmanCount(overlayWindow, m_settings.JetmanCount - 1);

        var exitItem = new NativeMenuItem
        {
            Header = "Exit ZXJetMen"
        };
        exitItem.Click += (_, _) => desktop.Shutdown();

        var menu = new NativeMenu();
        menu.Items.Add(m_jetmanCountItem);
        menu.Items.Add(m_addJetmanItem);
        menu.Items.Add(m_removeJetmanItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        var trayIcon = new TrayIcon
        {
            Icon = IconLoader.LoadWindowIcon(),
            IsVisible = true,
            Menu = menu,
            ToolTipText = "ZXJetMen"
        };

        if (OperatingSystem.IsMacOS())
        {
            MacOSProperties.SetIsTemplateIcon(trayIcon, false);
        }

        UpdateJetmanMenu();
        return trayIcon;
    }

    private void SetJetmanCount(OverlayWindow overlayWindow, int jetmanCount)
    {
        m_settings.JetmanCount = jetmanCount;
        m_settings.Save();
        overlayWindow.SetJetmanCount(m_settings.JetmanCount);
        UpdateJetmanMenu();
    }

    private void UpdateJetmanMenu()
    {
        m_jetmanCountItem.Header = $"Jetmen: {m_settings.JetmanCount}";
        m_addJetmanItem.IsEnabled = m_settings.JetmanCount < AppSettings.MaxJetmanCount;
        m_removeJetmanItem.IsEnabled = m_settings.JetmanCount > AppSettings.MinJetmanCount;
    }
}
