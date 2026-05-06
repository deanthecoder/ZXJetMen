[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)

# ZXJetMen

ZXJetMen is a tiny desktop companion inspired by the chunky, bright, wonderfully economical graphics of the ZX Spectrum classic Jetpac.

It drops little jetmen onto your desktop and lets them wander, fly, and chase collectible treasures across the tops of windows.

## Why

Most desktop companions either float above everything or ignore the desktop completely.

ZXJetMen is aiming for a more toy-like feel: the desktop becomes the playfield. On Windows the app samples visible top-level window edges and uses them as platforms. On macOS and other platforms it currently falls back to faint synthetic window blocks so the same movement can be tuned without needing native window enumeration straight away.

## What It Does

- Runs as a transparent Avalonia overlay.
- Uses Jetpac-inspired sprite sheets for walking, flying, and treasures.
- Lets spacemen walk along detected window tops on Windows.
- Provides faint synthetic platforms on non-Windows systems.
- Supports click-through overlay behavior on Windows and macOS.
- Runs the simulation at 15fps to keep the desktop overlay light.
- Adds a tray/menu-bar icon with an Exit command.
- Includes Windows `.ico` and macOS `.icns` app icons.

## Platform Notes

Windows has the richer prototype path at the moment because ZXJetMen can enumerate visible desktop windows through Win32 and DWM.

macOS support deliberately takes the simple route for now: the overlay is click-through, but platforms are generated inside the app instead of discovered from real system windows. Native macOS window enumeration is possible, but the CoreGraphics/Accessibility route is more invasive than this stage needs.

## Build and Run

Prereqs:

- .NET 9 SDK

Clone with submodules:

```bash
git clone --recurse-submodules https://github.com/deanthecoder/ZXJetMen.git
```

For an existing checkout:

```bash
git submodule update --init --recursive
```

Build and run:

```bash
dotnet build ZXJetMen.sln
dotnet run --project ZxJetMen.csproj
```

Use the tray/menu-bar icon to exit the app.

## Repo Layout

- `ZxJetMen.csproj` contains the Avalonia desktop app.
- `DTC.Core` is included as a shared local submodule.
- `Installer` is included from `DTC.Installer` for future packaging work.
- `Assets` contains the ZX Spectrum-style sprite sheets and app icons.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
