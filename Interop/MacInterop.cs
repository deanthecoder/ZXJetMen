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
using Avalonia.Controls;

namespace ZXJetMen.Interop;

/// <summary>
/// Applies macOS-specific behavior to the overlay window.
/// </summary>
/// <remarks>
/// Avalonia exposes the native handle, but ignoring mouse events still requires Objective-C messaging on macOS.
/// </remarks>
internal static partial class MacInterop
{
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

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static partial void objc_msgSend_bool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.Bool)] bool value);
}
