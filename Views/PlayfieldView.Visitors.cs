// Code authored by Dean Edis (DeanTheCoder).
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ZXJetMen.Models;

namespace ZXJetMen.Views;

public sealed partial class PlayfieldView
{
    private const double MinVisitorDelaySeconds = 30;
    private const double MaxVisitorDelaySeconds = 60;
    private readonly Bitmap m_willySheet = new(AssetLoader.Open(new Uri("avares://ZXJetMen/Assets/willy.png")));
    private readonly Bitmap m_horaceSheet = new(AssetLoader.Open(new Uri("avares://ZXJetMen/Assets/horace.png")));
    private WillyVisitor m_willy;
    private HoraceVisitor m_horace;
    private bool m_nextVisitorIsHorace = true;
    private double? m_nextVisitorAt;

    private void ResetVisitors()
    {
        m_willy = null;
        m_horace = null;
        m_nextVisitorIsHorace = true;
        m_nextVisitorAt = null;
    }

    private void StepVisitors(double dt, double now, IReadOnlyList<Platform> platforms)
    {
        m_nextVisitorAt ??= now + RandomVisitorDelay();
        if (m_willy is null && m_horace is null)
        {
            if (now < m_nextVisitorAt.Value)
                return;

            // Only spawn over ledges with a clear path from the top of the screen.
            var candidates = platforms.Where(p => p.Y >= 32 * m_spriteScale &&
                p.Y < Bounds.Height && p.Right - p.Left >= 64 * m_spriteScale).ToArray();
            foreach (var p in candidates.OrderBy(_ => m_random.Next()))
            {
                var left = Math.Max(0, p.Left) + 16 * m_spriteScale;
                var right = Math.Min(Bounds.Width, p.Right) - 24 * m_spriteScale;
                if (right < left)
                    continue;
                var x = left + m_random.NextDouble() * (right - left);
                if (platforms.Any(other => other.Y < p.Y && other.Right > x &&
                        other.Left < x + HoraceVisitor.Size * m_spriteScale))
                    continue;
                if (m_treasures.Any(t => t.Active && t.Grounded &&
                        Math.Abs(t.Y + TreasureHeight - p.Y) < 3 &&
                        t.X < x + HoraceVisitor.Size * m_spriteScale && t.X + TreasureWidth > x - 8 * m_spriteScale))
                    continue;

                var direction = m_random.Next(2) == 0 ? -1 : 1;
                if (m_nextVisitorIsHorace)
                    m_horace = new HoraceVisitor(x / m_spriteScale, direction, 8 + m_random.NextDouble() * 6);
                else
                    m_willy = new WillyVisitor(x / m_spriteScale, direction, 15 + m_random.NextDouble() * 15);
                m_nextVisitorIsHorace = !m_nextVisitorIsHorace;
                break;
            }

            if (m_willy is null && m_horace is null)
            {
                m_nextVisitorAt = now + 5;
                return;
            }
        }

        var nativePlatforms = platforms.Select(p => new Platform(p.Left / m_spriteScale,
            p.Right / m_spriteScale, p.Y / m_spriteScale, p.Bottom / m_spriteScale,
            p.ZOrder, p.IsSynthetic)).ToArray();
        var obstacles = m_treasures.Where(t => t.Active && t.Grounded)
            .Select(t => new WillyObstacle(t.X / m_spriteScale,
                (t.X + TreasureWidth) / m_spriteScale, (t.Y + TreasureHeight) / m_spriteScale)).ToArray();
        m_willy?.Step(dt, Bounds.Height / m_spriteScale, nativePlatforms, obstacles);
        m_horace?.Step(dt, Bounds.Width / m_spriteScale, Bounds.Height / m_spriteScale, nativePlatforms);
        if (m_willy?.Finished == true || m_horace?.Finished == true)
        {
            m_willy = null;
            m_horace = null;
            m_nextVisitorAt = now + RandomVisitorDelay();
        }
    }

    private double RandomVisitorDelay() => MinVisitorDelaySeconds +
        m_random.NextDouble() * (MaxVisitorDelaySeconds - MinVisitorDelaySeconds);

    private void DrawVisitors(DrawingContext context)
    {
        if (m_horace is { } horace)
        {
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
            context.DrawImage(m_horaceSheet, new Rect(horace.Frame * 24, 0, 24, 24),
                new Rect(Math.Round(horace.X * m_spriteScale), Math.Round(horace.Y * m_spriteScale),
                    24 * m_spriteScale, 24 * m_spriteScale));
        }
        DrawWilly(context);
    }

    private void DrawWilly(DrawingContext context)
    {
        if (m_willy is not { } willy)
            return;

        var frame = willy.Frame + (willy.Direction < 0 ? 4 : 0);
        // The original 16x16 frames contain the two-pixel walking offsets already.
        var x = willy.X - willy.Frame * 2 - (willy.Direction < 0 ? 2 : 0);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        context.DrawImage(m_willySheet, new Rect(frame * 16, 0, 16, 16),
            new Rect(Math.Round(x * m_spriteScale), Math.Round(willy.Y * m_spriteScale),
                16 * m_spriteScale, 16 * m_spriteScale));
    }
}
