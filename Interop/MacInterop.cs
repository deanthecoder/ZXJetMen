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
/// Applies macOS-specific behavior to the overlay window.
/// </summary>
/// <remarks>
/// Avalonia exposes the native handle, but ignoring mouse events still requires Objective-C messaging on macOS.
/// </remarks>
internal static partial class MacInterop
{
    private const int WindowListOptionOnScreenOnly = 1;
    private const int WindowListExcludeDesktopElements = 16;
    private const int NumberIntType = 9;
    private const int NumberDoubleType = 13;
    private const double TitlebarPlatformInset = 4;
    private static readonly IntPtr BoundsKey = CreateString("kCGWindowBounds");
    private static readonly IntPtr LayerKey = CreateString("kCGWindowLayer");
    private static readonly IntPtr AlphaKey = CreateString("kCGWindowAlpha");
    private static readonly IntPtr OwnerPidKey = CreateString("kCGWindowOwnerPID");

    public static void MakeClickThrough(Window window)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var handle = DesktopInterop.GetHandle(window);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        objc_msgSend_bool(handle, sel_registerName("setIgnoresMouseEvents:"), true);
        objc_msgSend_bool(handle, sel_registerName("setHasShadow:"), false);
    }

    public static IReadOnlyList<Platform> GetPlatforms(PixelRect screenBounds, double screenScale, out bool isFrontmostWindowFullscreen)
    {
        isFrontmostWindowFullscreen = false;
        if (!OperatingSystem.IsMacOS() || screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return [];
        }

        var windowInfo = CGWindowListCopyWindowInfo(
            WindowListOptionOnScreenOnly | WindowListExcludeDesktopElements,
            0);
        if (windowInfo == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var platforms = new List<Platform>();
            var screenLeft = screenBounds.X / screenScale;
            var screenTop = screenBounds.Y / screenScale;
            var screenRight = screenBounds.Right / screenScale;
            var screenBottom = screenBounds.Bottom / screenScale;
            var occluders = new List<CGRect>();
            var count = CFArrayGetCount(windowInfo);
            var zOrder = 0;

            for (var i = 0; i < count; i++)
            {
                var window = CFArrayGetValueAtIndex(windowInfo, i);
                if (!TryGetWindowRect(window, out var rect) ||
                    !IsUsableWindow(window, rect, screenLeft, screenTop, screenRight, screenBottom))
                {
                    continue;
                }

                var currentZOrder = zOrder++;
                if (currentZOrder == 0 && IsFullscreenWindow(rect, screenLeft, screenTop, screenRight, screenBottom))
                {
                    isFrontmostWindowFullscreen = true;
                }

                AddVisibleTopSegments(
                    platforms,
                    rect,
                    occluders,
                    screenLeft,
                    screenTop,
                    screenRight,
                    screenBottom,
                    currentZOrder);
                occluders.Add(rect);
            }

            return platforms;
        }
        finally
        {
            CFRelease(windowInfo);
        }
    }

    private static void AddVisibleTopSegments(
        List<Platform> platforms,
        CGRect rect,
        IReadOnlyList<CGRect> occluders,
        double screenLeft,
        double screenTop,
        double screenRight,
        double screenBottom,
        int zOrder)
    {
        var platformY = rect.Origin.Y + TitlebarPlatformInset;
        var bottom = Math.Min(rect.Origin.Y + rect.Size.Height, screenBottom) - screenTop;
        if (platformY < screenTop || platformY > screenBottom || bottom <= platformY - screenTop)
        {
            return;
        }

        var segments = new List<(double Left, double Right)>
        {
            (Math.Max(rect.Origin.X, screenLeft), Math.Min(rect.Origin.X + rect.Size.Width, screenRight))
        };

        foreach (var occluder in occluders)
        {
            if (occluder.Origin.Y > platformY || occluder.Origin.Y + occluder.Size.Height <= platformY)
            {
                continue;
            }

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var segment = segments[i];
                var coverLeft = Math.Max(segment.Left, occluder.Origin.X);
                var coverRight = Math.Min(segment.Right, occluder.Origin.X + occluder.Size.Width);
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

        var y = platformY - screenTop;
        foreach (var segment in segments.Where(s => s.Right - s.Left >= 32))
        {
            platforms.Add(new Platform(
                segment.Left - screenLeft,
                segment.Right - screenLeft,
                y,
                bottom,
                zOrder));
        }
    }

    private static bool IsUsableWindow(
        CGRect rect,
        double screenLeft,
        double screenTop,
        double screenRight,
        double screenBottom)
    {
        var width = rect.Size.Width;
        var height = rect.Size.Height;
        return width > 80 &&
               height > 40 &&
               rect.Origin.X < screenRight &&
               rect.Origin.X + width > screenLeft &&
               rect.Origin.Y < screenBottom &&
               rect.Origin.Y + height > screenTop;
    }

    private static bool IsUsableWindow(
        IntPtr window,
        CGRect rect,
        double screenLeft,
        double screenTop,
        double screenRight,
        double screenBottom)
    {
        return IsLayerZero(window) &&
               !IsOwnedByCurrentProcess(window) &&
               GetWindowAlpha(window) > 0 &&
               IsUsableWindow(rect, screenLeft, screenTop, screenRight, screenBottom);
    }

    private static bool IsFullscreenWindow(CGRect rect, double screenLeft, double screenTop, double screenRight, double screenBottom)
    {
        const double tolerance = 4;
        return rect.Origin.X <= screenLeft + tolerance &&
               rect.Origin.Y <= screenTop + tolerance &&
               rect.Origin.X + rect.Size.Width >= screenRight - tolerance &&
               rect.Origin.Y + rect.Size.Height >= screenBottom - tolerance;
    }

    private static bool TryGetWindowRect(IntPtr window, out CGRect rect)
    {
        rect = default;
        var bounds = CFDictionaryGetValue(window, BoundsKey);
        return bounds != IntPtr.Zero &&
               CGRectMakeWithDictionaryRepresentation(bounds, out rect);
    }

    private static bool IsLayerZero(IntPtr window)
    {
        var value = CFDictionaryGetValue(window, LayerKey);
        return value != IntPtr.Zero &&
               TryGetInt(value, out var layer) &&
               layer == 0;
    }

    private static bool IsOwnedByCurrentProcess(IntPtr window)
    {
        var value = CFDictionaryGetValue(window, OwnerPidKey);
        return value != IntPtr.Zero &&
               TryGetInt(value, out var ownerPid) &&
               ownerPid == Environment.ProcessId;
    }

    private static double GetWindowAlpha(IntPtr window)
    {
        var value = CFDictionaryGetValue(window, AlphaKey);
        return value == IntPtr.Zero || !TryGetDouble(value, out var alpha)
            ? 1
            : alpha;
    }

    private static bool TryGetInt(IntPtr number, out int value)
    {
        return CFNumberGetValue(number, NumberIntType, out value);
    }

    private static bool TryGetDouble(IntPtr number, out double value)
    {
        return CFNumberGetValue(number, NumberDoubleType, out value);
    }

    private static IntPtr CreateString(string value)
    {
        var result = CFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);
        if (result == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Could not create CFString for {value}.");
        }

        return result;
    }

    // ReSharper disable InconsistentNaming
    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static partial void objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.Bool)] bool value);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial IntPtr CGWindowListCopyWindowInfo(int option, uint relativeToWindow);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CGRectMakeWithDictionaryRepresentation(IntPtr dictionaryRepresentation, out CGRect rect);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial nint CFArrayGetCount(IntPtr array);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial IntPtr CFDictionaryGetValue(IntPtr dictionary, IntPtr key);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CFNumberGetValue(IntPtr number, int theType, out int value);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CFNumberGetValue(IntPtr number, int theType, out double value);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr CFStringCreateWithCString(IntPtr allocator, string cStr, uint encoding);

    [LibraryImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static partial void CFRelease(IntPtr cf);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }
    // ReSharper restore InconsistentNaming
}
