// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace ZXJetMen.Models;

/// <summary>
/// Describes a horizontal ledge that jetmen and treasures can land on.
/// </summary>
/// <remarks>
/// Platforms abstract over real window tops on Windows and synthetic ledges on other systems so the simulation does not care where footing came from.
/// </remarks>
public readonly record struct Platform(double Left, double Right, double Y, double Bottom, int ZOrder, bool IsSynthetic = false);
