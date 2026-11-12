// Code authored by Dean Edis (DeanTheCoder).
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace ZXJetMen.Models;

/// <summary>Walks with the original carried skis, then skis down a clear window side.</summary>
public sealed class HoraceVisitor
{
    public const int Size = 24;
    private const double TickSeconds = 1.0 / 30;
    private const double SlalomHalfWidth = 16;
    private double m_accumulator;
    private double m_carryRemaining;
    private int m_walkTicks;
    private double m_slopeCenter;
    private int m_slopeDirection;
    private int m_turnTicks;
    private Platform? m_launchPlatform;

    public HoraceVisitor(double x, int direction, double carrySeconds)
    {
        X = x;
        Y = -Size;
        Direction = direction;
        m_carryRemaining = carrySeconds;
    }

    public double X { get; private set; }
    public double Y { get; private set; }
    public int Direction { get; private set; }
    public bool Grounded { get; private set; }
    public bool Skiing { get; private set; }
    public bool Descending { get; private set; }
    public bool Finished { get; private set; }
    public bool LeavingWithSkis { get; private set; }
    public int Frame => Skiing
        ? Descending ? (m_turnTicks > 0 ? 6 : m_slopeDirection > 0 ? 5 : 7) : (Direction > 0 ? 4 : 8)
        : (Direction > 0 ? 0 : 2) + (m_walkTicks / 3 % 2);

    public void Rescale(double ratio)
    {
        X = (X + Size / 2.0) * ratio - Size / 2.0;
        Y = (Y + Size) * ratio - Size;
        m_slopeCenter = (m_slopeCenter + Size / 2.0) * ratio - Size / 2.0;
        // Re-evaluate room beside the window at the new sprite size.
        if (!Descending)
        {
            Skiing = false;
            m_launchPlatform = null;
        }
    }

    public void Step(double dt, double screenWidth, double screenHeight, IReadOnlyList<Platform> platforms)
    {
        m_accumulator += dt;
        while (m_accumulator + 1e-9 >= TickSeconds && !Finished)
        {
            m_accumulator -= TickSeconds;
            Tick(screenWidth, screenHeight, platforms);
        }
    }

    private void Tick(double screenWidth, double screenHeight, IReadOnlyList<Platform> platforms)
    {
        if (Descending)
        {
            if (m_turnTicks > 0)
                m_turnTicks--;
            // Use the original SE/S/SW steering poses, with no rotated or invented frames.
            var left = Math.Max(0, m_slopeCenter - SlalomHalfWidth);
            var right = Math.Min(screenWidth - Size, m_slopeCenter + SlalomHalfWidth);
            X = Math.Clamp(X + m_slopeDirection * 2, left, Math.Max(left, right));
            if (X <= left || X >= right)
            {
                m_slopeDirection = -m_slopeDirection;
                m_turnTicks = 2;
            }
            Y += 3;
            Finished = Y > screenHeight + Size;
            return;
        }

        if (LeavingWithSkis)
        {
            Y += 4;
            Finished = Y > screenHeight + Size;
            return;
        }

        var feet = Y + Size;
        var support = platforms.Where(p => Math.Abs(p.Y - feet) < 0.01 &&
                X + Size / 2.0 >= p.Left && X + Size / 2.0 <= p.Right)
            .OrderBy(p => p.ZOrder).Select(p => (Platform?)p).FirstOrDefault();

        if (Skiing && m_launchPlatform is { } launch)
        {
            // If the window moves or closes, stop gliding on its old top.
            if (!platforms.Contains(launch))
            {
                Skiing = false;
                Grounded = false;
            }
            else
            {
                X += Direction * 3;
                var target = Direction > 0 ? launch.Right + SlalomHalfWidth : launch.Left - Size - SlalomHalfWidth;
                if (Direction > 0 ? X >= target : X <= target)
                {
                    X = target;
                    m_slopeCenter = target;
                    m_slopeDirection = Direction;
                    Grounded = false;
                    Descending = true;
                }
                return;
            }
        }

        if (Grounded && support is { } ledge)
        {
            m_carryRemaining -= TickSeconds;
            var remaining = Direction > 0 ? ledge.Right - (X + Size) : X - ledge.Left;
            if (m_carryRemaining <= 0 && remaining <= 32 && HasSkiRoom(ledge, screenWidth, platforms))
            {
                Skiing = true;
                m_launchPlatform = ledge;
                return; // Direct switch between original carrying and skiing poses.
            }
            if (remaining <= 2)
            {
                if (m_carryRemaining < -30)
                {
                    // No skiing space appeared: leave carrying the skis rather than linger forever.
                    X = Direction > 0 ? ledge.Right : ledge.Left - Size;
                    Grounded = false;
                    LeavingWithSkis = true;
                    return;
                }
                Direction = -Direction;
                return;
            }
            X += Direction;
            m_walkTicks++;
            return;
        }

        Grounded = false;
        Y += 4;
        var landing = platforms.Where(p => feet <= p.Y && Y + Size >= p.Y &&
                X + Size / 2.0 >= p.Left && X + Size / 2.0 <= p.Right)
            .OrderBy(p => p.Y).ThenBy(p => p.ZOrder).Select(p => (Platform?)p).FirstOrDefault();
        if (landing is { } landed)
        {
            Y = landed.Y - Size;
            Grounded = true;
        }
        Finished = Y > screenHeight + Size;
    }

    private bool HasSkiRoom(Platform ledge, double screenWidth, IReadOnlyList<Platform> platforms)
    {
        var left = Direction > 0 ? ledge.Right : ledge.Left - Size - 2 * SlalomHalfWidth;
        var right = left + Size + 2 * SlalomHalfWidth;
        if (left < 0 || right > screenWidth)
            return false;
        // Keep the descent beside this window clear of other visible window rectangles.
        return !platforms.Any(p => p != ledge && p.Left < right && p.Right > left &&
            p.Y < ledge.Bottom && p.Bottom > ledge.Y);
    }
}
