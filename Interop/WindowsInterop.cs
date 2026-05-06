// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using ZXJetMen.Models;

namespace ZXJetMen.Interop;

/// <summary>
/// Extracts desktop platform geometry from native Windows windows.
/// </summary>
/// <remarks>
/// Win32 enumeration and DWM visibility filtering are isolated here so the simulation receives clean platform segments.
/// </remarks>
internal static partial class WindowsInterop
{
    public static IReadOnlyList<Platform> GetPlatforms(PixelRect screenBounds, IntPtr self)
    {
        if (!OperatingSystem.IsWindows() || screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return Array.Empty<Platform>();
        }

        var platforms = new List<Platform>();
        var occluders = new List<NativeRect>();
        var zOrder = 0;

        // EnumWindows returns top-level windows from front to back.
        EnumWindows((hwnd, _) =>
        {
            if (hwnd == self || !IsUsableWindow(hwnd))
            {
                return true;
            }

            if (GetWindowRect(hwnd, out var rect))
            {
                var currentZOrder = zOrder++;
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width <= 80 ||
                    height <= 40 ||
                    rect.Bottom <= screenBounds.Y ||
                    rect.Top >= screenBounds.Bottom ||
                    rect.Right <= screenBounds.X ||
                    rect.Left >= screenBounds.Right)
                {
                    return true;
                }

                if (rect.Top >= screenBounds.Y && rect.Top <= screenBounds.Bottom)
                {
                    // Add only the top-edge portions not covered by higher Z-order windows.
                    AddVisibleTopSegments(platforms, rect, occluders, screenBounds, currentZOrder);
                }

                occluders.Add(rect);
            }

            return true;
        }, IntPtr.Zero);

        return platforms;
    }

    private static bool IsUsableWindow(IntPtr hwnd)
    {
        // Filter out minimized, invisible, tool, and cloaked shell windows.
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindowTextLength(hwnd) == 0)
        {
            return false;
        }

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        return true;
    }

    private static void AddVisibleTopSegments(
        List<Platform> platforms,
        NativeRect rect,
        IReadOnlyList<NativeRect> occluders,
        PixelRect screenBounds,
        int zOrder)
    {
        // Split the top edge around windows that are in front of this one.
        var segments = new List<(int Left, int Right)>
        {
            (Math.Max(rect.Left, screenBounds.X), Math.Min(rect.Right, screenBounds.Right))
        };

        foreach (var occluder in occluders)
        {
            if (occluder.Top > rect.Top || occluder.Bottom <= rect.Top)
            {
                continue;
            }

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var segment = segments[i];
                var coverLeft = Math.Max(segment.Left, occluder.Left);
                var coverRight = Math.Min(segment.Right, occluder.Right);
                if (coverLeft >= coverRight)
                {
                    continue;
                }

                segments.RemoveAt(i);
                if (segment.Left < coverLeft)
                {
                    segments.Add((segment.Left, coverLeft));
                }

                if (coverRight < segment.Right)
                {
                    segments.Add((coverRight, segment.Right));
                }
            }
        }

        var y = rect.Top - screenBounds.Y;
        var bottom = Math.Min(rect.Bottom, screenBounds.Bottom) - screenBounds.Y;
        foreach (var segment in segments.Where(s => s.Right - s.Left >= 32))
        {
            platforms.Add(new Platform(
                segment.Left - screenBounds.X,
                segment.Right - screenBounds.X,
                y,
                bottom,
                zOrder));
        }
    }

    private static IntPtr GetHandle(Window window)
    {
        return OperatingSystem.IsWindows()
            ? window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero
            : IntPtr.Zero;
    }

    public static void MakeClickThrough(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = GetHandle(window);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        // Remove native chrome and make the overlay transparent to mouse input.
        var normalStyle = GetWindowLong(handle, GWL_STYLE);
        normalStyle &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER | WS_DLGFRAME);
        normalStyle |= WS_POPUP;
        _ = SetWindowLong(handle, GWL_STYLE, normalStyle);

        var style = GetWindowLong(handle, GWL_EXSTYLE);
        _ = SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
        DisableShadow(handle);
        _ = SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    private static void DisableShadow(IntPtr handle)
    {
        // Suppress the DWM non-client shadow around the transparent overlay.
        var policy = DwmNcrpDisabled;
        _ = DwmSetWindowAttribute(handle, DwmwaNcrenderingPolicy, ref policy, sizeof(int));

        var margins = new Margins();
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);
    }

    // ReSharper disable InconsistentNaming
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_FRAMECHANGED = 0x0020;
    private const int DwmwaNcrenderingPolicy = 2;
    private const int DwmwaCloaked = 14;
    private const int DwmNcrpDisabled = 1;
    // ReSharper restore InconsistentNaming

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    // ReSharper disable once UnusedMethodReturnValue.Local
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        int uFlags);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset);

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    // ReSharper disable InconsistentNaming
    /// <summary>
    /// Mirrors the Win32 RECT structure returned by user32.
    /// </summary>
    /// <remarks>
    /// Keeping this local to the interop layer avoids leaking native coordinate details into platform discovery code.
    /// </remarks>
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Mirrors the DWM margins structure used when adjusting the overlay frame.
    /// </summary>
    /// <remarks>
    /// The transparent overlay needs native frame tweaks on Windows, and this struct keeps that call strongly typed.
    /// </remarks>
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }
    // ReSharper restore InconsistentNaming
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
}
