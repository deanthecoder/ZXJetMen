# Horace sprite provenance

`horace.png` contains original ZX Spectrum **Horace Goes Skiing** graphics
(1982), William Tang / Beam Software, published by Melbourne House.
The original artwork belongs to its respective rights holders and is not
covered by this repository's MIT licence.

Extracted from the game bytes in [Ritchie333's annotated disassembly](https://github.com/Ritchie333/skiing/blob/166945789eacfcd640aaea9c16501cff9338d70a/src/skiing.skool).
Source file SHA-256:
`3304865263d35c540da7d0b5645ea4ea6451c18753800688138849fb74b9e0c6`.

The sheet has nine 24×24 cells, in this order:

| Cell | Original address | Pose | Original dimensions |
| --- | --- | --- | --- |
| 0 | 0x6138 | Carrying skis, right, frame 1 | 24×16 |
| 1 | 0x6200 | Carrying skis, right, frame 2 | 24×16 |
| 2 | 0x6198 | Carrying skis, left, frame 1 | 24×16 |
| 3 | 0x6260 | Carrying skis, left, frame 2 | 24×16 |
| 4 | 0x670C | Skiing east | 24×24 |
| 5 | 0x6754 | Skiing southeast | 24×24 |
| 6 | 0x679C | Skiing south | 24×24 |
| 7 | 0x67E4 | Skiing southwest | 24×24 |
| 8 | 0x682C | Skiing west | 24×24 |

Each group of eight bytes is one 8×8 tile, with the most significant bit at
the left. Tile rows are stored bottom first; tiles within a row are left to
right. Carrying poses have eight transparent padding rows above them.
Set bits are rendered cyan and unset bits transparent. The shapes are
unchanged: no mirroring, rotation, redrawing, or new animation frames.
The cyan ink is a desktop colour choice, not a reproduction of the game's
varying screen attributes.

Window patrols, the direct carrying-to-skiing switch, and the downhill path
beside a window are desktop behaviours. They do not emulate the original
game's physics. Narrow or obstructed sides keep Horace carrying his skis;
after an extra 30 seconds without a suitable route, he leaves carrying them.
