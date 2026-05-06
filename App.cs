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

namespace ZXJetMen;

/// <summary>
/// Wires the desktop lifetime to the overlay window and tray icon.
/// </summary>
/// <remarks>
/// The application has no normal main window chrome, so this class owns the shutdown path users need from the tray or menu bar.
/// </remarks>
public sealed class App : Application
{
    private TrayIcon m_trayIcon;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new OverlayWindow();
            m_trayIcon = CreateTrayIcon(desktop);
            desktop.Exit += (_, _) => m_trayIcon?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var exitItem = new NativeMenuItem
        {
            Header = "Exit ZXJetMen"
        };
        exitItem.Click += (_, _) => desktop.Shutdown();

        var menu = new NativeMenu();
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

        return trayIcon;
    }
}
