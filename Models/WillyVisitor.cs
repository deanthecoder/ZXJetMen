// Code authored by Dean Edis (DeanTheCoder).
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace ZXJetMen.Models;

/// <summary>A visiting Willy, using Spectrum pixel steps rather than jetman physics.</summary>
public sealed class WillyVisitor
{
    public const double TickSeconds = 1.0 / 15;
    public const int Height = 16;
    public const int Width = 8;
    private double m_accumulator;
    private double m_walkRemaining;
    private int m_jumpStep = -1;
    private bool m_hasLanded;

    public WillyVisitor(double x, int direction, double walkSeconds)
    {
        X = x;
        Y = -Height;
        Direction = direction;
        m_walkRemaining = walkSeconds;
    }

    // Coordinates and obstacles are in native Spectrum pixels, independent of display scale.
    public double X { get; private set; }
    public double Y { get; private set; }
    public int Direction { get; private set; }
    public int Frame { get; private set; }
    public bool Grounded { get; private set; }
    public bool Departing { get; private set; }
    public bool Finished { get; private set; }

    public void Rescale(double ratio)
    {
        X = (X + Width / 2.0) * ratio - Width / 2.0;
        Y = (Y + Height) * ratio - Height;
    }

    public void Step(double dt, double screenHeight, IReadOnlyList<Platform> platforms,
        IReadOnlyList<WillyObstacle> obstacles)
    {
        m_accumulator += dt;
        while (m_accumulator + 1e-9 >= TickSeconds && !Finished)
        {
            m_accumulator -= TickSeconds;
            Tick(screenHeight, platforms, obstacles);
        }
    }

    private void Tick(double screenHeight, IReadOnlyList<Platform> platforms, IReadOnlyList<WillyObstacle> obstacles)
    {
        if (m_hasLanded)
            m_walkRemaining -= TickSeconds;

        var feet = Y + Height;
        var support = platforms.Where(p => Math.Abs(p.Y - feet) < 0.01 &&
                X + Width / 2.0 >= p.Left && X + Width / 2.0 <= p.Right)
            .OrderBy(p => p.ZOrder).Select(p => (Platform?)p).FirstOrDefault();

        if (Grounded && support is null)
            Grounded = false;

        if (Grounded && support is { } ledge)
        {
            var edgeDistance = Direction > 0 ? ledge.Right - (X + Width) : X - ledge.Left;
            if (edgeDistance <= 4)
            {
                if (m_walkRemaining <= 0)
                {
                    Departing = true;
                    StartJump();
                }
                else
                {
                    Direction = -Direction;
                    return; // Turning takes a tick in the original game.
                }
            }
            else if (NeedsJump(obstacles, ledge))
            {
                // Don't start a hurdle that would carry him off his ledge prematurely.
                if (edgeDistance >= 38 || m_walkRemaining <= 0)
                    StartJump();
                else
                {
                    Direction = -Direction;
                    return;
                }
            }
        }

        var previousFeet = Y + Height;
        if (m_jumpStep >= 0)
        {
            // The original y-coordinate has half-pixel units: ((J & 254) - 8) / 2.
            Y += (m_jumpStep / 2) - 4;
            MoveHorizontally();
            if (++m_jumpStep == 18)
                m_jumpStep = -1;
        }
        else if (Grounded)
        {
            MoveHorizontally();
        }
        else
        {
            Y += 4; // Original falling is vertical, at four pixels per game step.
        }

        // A final hurdle can carry him beyond the edge before the normal exit trigger.
        if (m_walkRemaining <= 0 && support is { } departureLedge &&
            (X + Width / 2.0 < departureLedge.Left || X + Width / 2.0 > departureLedge.Right))
            Departing = true;

        if (!Grounded && !Departing && Y + Height >= previousFeet)
        {
            var landing = platforms.Where(p => p.Y >= previousFeet && p.Y <= Y + Height &&
                    X + Width / 2.0 >= p.Left && X + Width / 2.0 <= p.Right)
                .OrderBy(p => p.Y).ThenBy(p => p.ZOrder).Select(p => (Platform?)p).FirstOrDefault();
            if (landing is { } platform)
            {
                Y = platform.Y - Height;
                Grounded = true;
                m_hasLanded = true;
                m_jumpStep = -1;
            }
        }

        Finished = Y > screenHeight + Height;
    }

    private bool NeedsJump(IReadOnlyList<WillyObstacle> obstacles, Platform ledge)
    {
        return obstacles.Any(o => Math.Abs(o.Bottom - ledge.Y) < 1 &&
            (Direction > 0 ? o.Left - (X + Width) : X - o.Right) is >= 0 and <= 8);
    }

    private void StartJump()
    {
        Grounded = false;
        m_jumpStep = 0;
    }

    private void MoveHorizontally()
    {
        X += Direction * 2;
        Frame = (Frame + Direction + 4) % 4;
    }
}

public readonly record struct WillyObstacle(double Left, double Right, double Bottom);
