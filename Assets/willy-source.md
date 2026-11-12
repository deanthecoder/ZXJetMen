# Willy sprite provenance

`willy.png` contains the eight original 16×16 monochrome Willy frames from
ZX Spectrum Jet Set Willy (1984), Matthew Smith / Software Projects Ltd.
White pixels replace the original ink; unset bits are transparent. No redraw,
mirroring, interpolation or generated artwork is used.

Source bytes: addresses 40192–40447, in Richard Dymond's annotated disassembly:
https://skoolkid.github.io/jetsetwilly/dec/asm/40192.html
https://github.com/skoolkid/jetsetwilly/blob/master/sources/jsw.skool

Frames 0–3 face right; frames 4–7 face left and play in reverse order.
Each frame stores 16 pairs of bytes, most significant bit first.
The original art remains the property of its respective rights holders and
is not covered by this repository's MIT licence.

Movement reference:
https://skoolkid.github.io/jetsetwilly/dec/asm/36307.html
https://skoolkid.github.io/jetsetwilly/dec/asm/36796.html

The visitor uses two-pixel horizontal steps and the original 18 vertical jump
deltas: -4,-4,-3,-3,-2,-2,-1,-1,0,0,1,1,2,2,3,3,4,4.
Falling is vertical at four pixels per tick. A fixed 15 Hz cadence suits the
desktop simulation; the original game's wall-clock cadence varies by room.
Autonomous decisions, safe arrival after long falls, arbitrary window-top
landings and ignoring platforms during the final exit are desktop adaptations.
