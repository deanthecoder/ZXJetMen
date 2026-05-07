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
using Avalonia.Markup.Xaml;
using DTC.Core.Commands;
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
    private TrayIcons m_trayIcons;
    private TrayIcon m_trayIcon;
    private NativeMenuItem m_addJetmanItem;
    private NativeMenuItem m_removeJetmanItem;
    private NativeMenuItem m_jetmanCountItem;
    private NativeMenuItem m_miniModeItem;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var overlayWindow = new OverlayWindow(m_settings.JetmanCount, m_settings.MiniMode);
            desktop.MainWindow = overlayWindow;
            m_trayIcon = CreateTrayIcon(desktop, overlayWindow);
            m_trayIcons = [m_trayIcon];
            TrayIcon.SetIcons(this, m_trayIcons);
            desktop.Exit += (_, _) =>
            {
                TrayIcon.SetIcons(this, null);
                m_trayIcon?.Dispose();
                m_settings.Save();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, OverlayWindow overlayWindow)
    {
        m_jetmanCountItem = new NativeMenuItem(GetJetmanCountHeader())
        {
            IsEnabled = false
        };

        m_addJetmanItem = new NativeMenuItem("Add Jetman")
        {
            Command = new RelayCommand(_ => SetJetmanCount(overlayWindow, m_settings.JetmanCount + 1))
        };

        m_removeJetmanItem = new NativeMenuItem("Remove Jetman")
        {
            Command = new RelayCommand(_ => SetJetmanCount(overlayWindow, m_settings.JetmanCount - 1))
        };

        m_miniModeItem = new NativeMenuItem("Mini mode")
        {
            Command = new RelayCommand(_ => SetMiniMode(overlayWindow, !m_settings.MiniMode)),
            ToggleType = NativeMenuItemToggleType.CheckBox
        };

        var menu = new NativeMenu
        {
            m_jetmanCountItem,
            m_addJetmanItem,
            m_removeJetmanItem,
            new NativeMenuItemSeparator(),
            m_miniModeItem,
            new NativeMenuItemSeparator(),
            new NativeMenuItem("Exit")
            {
                ToolTip = "Exit",
                Command = new RelayCommand(_ => desktop.Shutdown())
            }
        };
        menu.NeedsUpdate += (_, _) => UpdateJetmanMenu();

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

    private void SetMiniMode(OverlayWindow overlayWindow, bool miniMode)
    {
        m_settings.MiniMode = miniMode;
        m_settings.Save();
        overlayWindow.SetMiniMode(m_settings.MiniMode);
        UpdateJetmanMenu();
    }

    private void UpdateJetmanMenu()
    {
        m_jetmanCountItem.Header = GetJetmanCountHeader();
        m_addJetmanItem.IsEnabled = m_settings.JetmanCount < AppSettings.MaxJetmanCount;
        m_removeJetmanItem.IsEnabled = m_settings.JetmanCount > AppSettings.MinJetmanCount;
        m_miniModeItem.IsChecked = m_settings.MiniMode;
    }

    private string GetJetmanCountHeader() => $"Jetmen: {m_settings.JetmanCount}";
}
