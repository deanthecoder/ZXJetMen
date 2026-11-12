[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)
[![GitHub Repo stars](https://img.shields.io/github/stars/deanthecoder/ZXJetMen?style=social&label=Star)](https://github.com/deanthecoder/ZXJetMen/stargazers)

# ZXJetMen

Tiny Jetpac men loose on your desktop.

ZXJetMen is a little retro desktop toy inspired by the bright, chunky, beautifully economical graphics of the ZX Spectrum classic Jetpac.

It drops tiny jetmen onto your desktop, lets treasures tumble in from above, and turns your windows into platforms for a miniature 8-bit scramble. They walk, thrust, fall, chase treasure, and occasionally make a charming mess of the place.

![ZXJetMen running on the desktop](img/zxjetmen-demo.gif)

## What You Get

- Jetpac-inspired spacemen and treasure sprites.
- Little jetmen walking, flying, and collecting whatever treasure they bump into.
- Occasional visits from Jet Set Willy: original Spectrum sprites and stepped movement, treasure hurdles, and a final leap off a window.
- Cyan Horace carries his skis along a window, then slaloms down beside it when there is room, using original Horace Goes Skiing frames.
- A smoke puff when they blast off from a platform.
- A tray/menu-bar icon with controls for adding or removing jetmen.
- A mini mode for native-size sprites, and the regular chunky 2x mode.
- Windows desktop window edges used as platforms.
- Faint built-in platforms on macOS and other systems.
- Installers for people who just want to run it.

## The Vibe

This is for anyone who still has affection for single-screen arcade chaos, harsh little pixels, and the ZX Spectrum palette doing far more work than it had any right to.

ZXJetMen is not a game remake. It is more like a tiny desktop toy box: a handful of little space-lads wandering around while you get on with other things.

## Getting It

If you just want to play with it, grab the installer for your platform from the project releases.

Once running, use the tray/menu-bar icon to:

- Add or remove jetmen.
- Toggle mini mode.
- Exit cleanly.

## Build From Source

You will need the .NET 9 SDK.

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

## Notes

ZXJetMen is a fan-made desktop toy and is not affiliated with the original Jetpac or its creators.

Horace and Willy take turns visiting, starting with Horace after 30–60 seconds
and leaving the same gap between visits when a suitable exposed window top is available.
Willy wanders for 15–30 seconds before heading off the edge. Horace carries his
skis for 8–14 seconds before looking for a clear side to ski down; when there
isn't room, he keeps carrying them and eventually leaves. Both respect mini mode.
Original sprite credits and references are in [Assets/willy-source.md](Assets/willy-source.md)
and [Assets/horace-source.md](Assets/horace-source.md).

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
