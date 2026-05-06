// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using ZXJetMen.Models;

namespace ZXJetMen.Views;

/// <summary>
/// Renders and advances the ZXJetMen playfield.
/// </summary>
/// <remarks>
/// This control owns the small game simulation: jetman movement, treasure spawning, sprite animation, and platform collisions.
/// </remarks>
public sealed class PlayfieldView : Control
{
    private const int SpriteSheetColumns = 4;
    private const int SpriteSheetRows = 3;
    private const int WalkFrameCount = 4;
    private const int FlyFrameCount = 4;
    private const int TreasureFrameCount = 4;
    private const int SmokeCellCount = 3;
    private const int SmokeAnimationFrameCount = 5;
    private const int WalkRow = 0;
    private const int FlyRow = 1;
    private const int TreasureRow = 2;
    private const int TreasureTintChoices = 5;
    private const int RedTintIndex = 0;
    private const int CyanTintIndex = 2;
    private const int GreenTintIndex = 3;
    private const int YellowTintIndex = 4;
    private const double SpriteScale = 2;
    private const double Gravity = 100;
    private const double FlyChancePerSecond = 0.08;
    private const double FlyDurationSeconds = 1.0;
    private const double EdgeFlyDurationSeconds = 0.5;
    private const double FlyThrust = 200;
    private const double MaxUpwardSpeed = -100;
    private const double SimulationFramesPerSecond = 15;
    private const double HorizontalAcceleration = 320;
    private const double GroundFriction = 7;
    private const double AirFriction = 1.5;
    private const double MinTreasureY = 100;
    private const double TopSpawnClearance = 100;
    private const double TreasureArrivalDistance = 18;
    private const double TreasurePickupTopInsetRatio = 0.5;
    private const double TreasureSteerDeadZone = 12;
    private const double ClimbThreshold = 18;
    private const double DescendThreshold = 10;
    private const double SlowFallDistance = 80;
    private const double MaxFallSpeedNearTreasure = 80;
    private const double DirectionChangeCooldownSeconds = 1;
    private const double MinSpawnDelaySeconds = 1;
    private const double MaxSpawnDelaySeconds = 2;
    private readonly Random m_random = new();
    private readonly List<Jetman> m_jetmen = new();
    private readonly List<Treasure> m_treasures = new();
    private bool m_treasuresInitialized;
    private bool m_jetmanSpawningStarted;
    private int m_jetmanLimit;
    private double m_nextJetmanSpawnAt;

    // 4x3 sheet: walk row, fly row, treasure row.
    private static readonly Uri SpriteSheetUri = new("avares://ZXJetMen/Assets/sheet.png");
    private static readonly Uri SmokeSheetUri = new("avares://ZXJetMen/Assets/smoke.png");
    private static readonly IBrush SyntheticPlatformFill = new SolidColorBrush(Color.FromArgb(18, 0, 212, 255));
    private static readonly IPen SyntheticPlatformStroke = new Pen(new SolidColorBrush(Color.FromArgb(42, 255, 236, 68)));
    private static readonly int[] SmokeSequence = [2, 1, 0, 1, 2];
    private readonly Bitmap m_spriteSheet = new(AssetLoader.Open(SpriteSheetUri));
    private readonly Bitmap m_smokeSheet = new(AssetLoader.Open(SmokeSheetUri));
    private readonly Bitmap[,] m_treasureSprites;
    private double CellWidth => m_spriteSheet.Size.Width / SpriteSheetColumns;
    private double CellHeight => m_spriteSheet.Size.Height / SpriteSheetRows;
    private double SmokeCellWidth => m_smokeSheet.Size.Width;
    private double SmokeCellHeight => m_smokeSheet.Size.Height / SmokeCellCount;
    private double SmokeWidth => SmokeCellWidth * SpriteScale;
    private double SmokeHeight => SmokeCellHeight * SpriteScale;
    private double JetmanWidth => CellWidth * SpriteScale;
    private double JetmanHeight => CellHeight * SpriteScale;
    private double TreasureWidth => CellWidth * SpriteScale;
    private double TreasureHeight => CellHeight * SpriteScale;
    private double TreasurePickupTopInset => TreasureHeight * TreasurePickupTopInsetRatio;
    private double TreasurePickupHeight => TreasureHeight - TreasurePickupTopInset;
    private double WalkSpeed => JetmanWidth * SimulationFramesPerSecond / WalkFrameCount;

    public bool ShowSyntheticPlatforms { get; set; }

    public PlayfieldView(int jetmanLimit)
    {
        m_treasureSprites = CreateTreasureSprites();
        SetJetmanLimit(jetmanLimit);
    }

    public int JetmanLimit => m_jetmanLimit;

    public void Reset()
    {
        m_jetmen.Clear();
        m_treasures.Clear();
        m_treasuresInitialized = false;
        m_jetmanSpawningStarted = false;
        m_nextJetmanSpawnAt = 0;
        InvalidateVisual();
    }

    public void Step(double dt, double now, IReadOnlyList<Platform> platforms)
    {
        m_lastKnownPlatforms = platforms;

        if (!m_treasuresInitialized)
        {
            InitializeTreasures(now);
        }
        else
        {
            ReconcileTreasureCount(now);
        }

        foreach (var treasure in m_treasures)
        {
            StepTreasure(treasure, dt, now, platforms);
        }

        if (m_treasures.Count > 0 && m_treasures.All(t => t.Active && t.Grounded))
        {
            SpawnNextJetman(now);
        }

        foreach (var jetman in m_jetmen.ToArray())
        {
            StepJetman(jetman, dt, now, platforms);
        }

        InvalidateVisual();
    }

    public void SetJetmanLimit(int jetmanLimit)
    {
        m_jetmanLimit = Math.Max(1, jetmanLimit);
        TrimExcessJetmenAndTreasures();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        DrawSyntheticPlatforms(context);

        foreach (var treasure in m_treasures)
        {
            DrawTreasure(context, treasure);
        }

        foreach (var jetman in m_jetmen)
        {
            DrawSmoke(context, jetman);
            DrawJetman(context, jetman);
        }
    }

    private void DrawSyntheticPlatforms(DrawingContext context)
    {
        if (!ShowSyntheticPlatforms)
        {
            return;
        }

        foreach (var platform in m_lastKnownPlatforms.Where(s => s.IsSynthetic))
        {
            var rect = new Rect(platform.Left, platform.Y, platform.Right - platform.Left, platform.Bottom - platform.Y);
            context.DrawRectangle(SyntheticPlatformFill, SyntheticPlatformStroke, rect);
        }
    }

    private void DrawTreasure(DrawingContext context, Treasure treasure)
    {
        if (!treasure.Active)
        {
            return;
        }

        var dest = new Rect(treasure.X, treasure.Y, TreasureWidth, TreasureHeight);
        context.DrawImage(m_treasureSprites[treasure.SpriteIndex, treasure.TintIndex], dest);
    }

    private void DrawSmoke(DrawingContext context, Jetman jetman)
    {
        if (jetman.SmokeAnimationTime >= SmokeAnimationFrameCount / SimulationFramesPerSecond)
        {
            return;
        }

        var animationFrame = Math.Min(
            SmokeAnimationFrameCount - 1,
            (int)Math.Floor(jetman.SmokeAnimationTime * SimulationFramesPerSecond));
        var sourceRow = SmokeSequence[animationFrame];
        var source = new Rect(0, sourceRow * SmokeCellHeight, SmokeCellWidth, SmokeCellHeight);
        var dest = new Rect(
            jetman.SmokeAnchorX - SmokeWidth / 2,
            jetman.SmokeAnchorY - SmokeHeight,
            SmokeWidth,
            SmokeHeight);

        context.DrawImage(m_smokeSheet, source, dest);
    }

    private void DrawJetman(DrawingContext context, Jetman jetman)
    {
        var airborne = !jetman.Grounded || jetman.FlyTimeRemaining > 0;
        var frameCount = airborne ? FlyFrameCount : WalkFrameCount;
        var row = airborne ? FlyRow : WalkRow;

        // Old-school sprite cadence: one full walk cycle moves exactly one sprite width.
        var frame = airborne
            ? (int)Math.Floor(jetman.FlyAnimationTime * 12) % frameCount
            : jetman.Grounded
                ? GetWalkFrame(jetman)
                : 0;
        var source = GetSheetSource(frame, row);
        var dest = new Rect(jetman.X, jetman.Y, JetmanWidth, JetmanHeight);

        if ((Math.Abs(jetman.Vx) > 1 ? jetman.Vx : jetman.IntentDirection) > 0)
        {
            // The source art faces left, so mirror it for rightward walking.
            using (context.PushTransform(Matrix.CreateScale(-1, 1)))
            {
                context.DrawImage(m_spriteSheet, source, new Rect(-(jetman.X + JetmanWidth), jetman.Y, JetmanWidth, JetmanHeight));
            }
        }
        else
        {
            context.DrawImage(m_spriteSheet, source, dest);
        }
    }

    private Rect GetSheetSource(int column, int row)
    {
        return new Rect(column * CellWidth, row * CellHeight, CellWidth, CellHeight);
    }

    private static Bitmap[,] CreateTreasureSprites()
    {
        var treasureSprites = new Bitmap[TreasureFrameCount, TreasureTintChoices];
        var tints = new[]
        {
            new SKColor(255, 0, 0),
            new SKColor(255, 255, 255),
            new SKColor(0, 255, 255),
            new SKColor(0, 255, 0),
            new SKColor(255, 255, 0)
        };

        using var stream = AssetLoader.Open(SpriteSheetUri);
        using var sheet = SKBitmap.Decode(stream);
        var cellWidth = sheet.Width / SpriteSheetColumns;
        var cellHeight = sheet.Height / SpriteSheetRows;

        for (var column = 0; column < TreasureFrameCount; column++)
        {
            for (var tintIndex = 0; tintIndex < TreasureTintChoices; tintIndex++)
            {
                treasureSprites[column, tintIndex] = CreateTintedTreasureSprite(sheet, column, cellWidth, cellHeight, tints[tintIndex]);
            }
        }

        return treasureSprites;
    }

    private static Bitmap CreateTintedTreasureSprite(SKBitmap sheet, int column, int cellWidth, int cellHeight, SKColor tint)
    {
        var imageInfo = new SKImageInfo(cellWidth, cellHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var output = new SKBitmap(imageInfo);
        output.Erase(SKColors.Transparent);

        for (var y = 0; y < cellHeight; y++)
        {
            for (var x = 0; x < cellWidth; x++)
            {
                var source = sheet.GetPixel(column * cellWidth + x, TreasureRow * cellHeight + y);
                if (source.Alpha == 0)
                {
                    output.SetPixel(x, y, SKColors.Transparent);
                    continue;
                }

                var intensity = Math.Max(source.Red, Math.Max(source.Green, source.Blue));
                output.SetPixel(
                    x,
                    y,
                    new SKColor(
                        ScaleTintChannel(tint.Red, intensity),
                        ScaleTintChannel(tint.Green, intensity),
                        ScaleTintChannel(tint.Blue, intensity),
                        source.Alpha));
            }
        }

        using var image = SKImage.FromBitmap(output);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var encoded = new MemoryStream(data.ToArray());
        return new Bitmap(encoded);
    }

    private static byte ScaleTintChannel(byte channel, byte intensity)
    {
        return (byte)(channel * intensity / 255);
    }

    private void StepJetman(Jetman jetman, double dt, double now, IReadOnlyList<Platform> platforms)
    {
        var previousBottom = jetman.Y + JetmanHeight;
        jetman.DirectionCooldown = Math.Max(0, jetman.DirectionCooldown - dt);
        jetman.SmokeAnimationTime += dt;
        var currentSupport = jetman.Retiring ? null : FindSupport(jetman, jetman.X, platforms);
        if (jetman.Retiring)
        {
            jetman.Treasure = null;
            jetman.Grounded = false;
            jetman.FlyTimeRemaining = 0;
        }
        else
        {
            UpdateTreasure(jetman);
            SteerToTreasure(jetman, currentSupport);
        }

        if (!jetman.Retiring &&
            jetman.FlyTimeRemaining <= 0 &&
            m_random.NextDouble() < FlyChancePerSecond * dt)
        {
            StartFlying(jetman, FlyDurationSeconds);
            jetman.Grounded = false;
        }

        if (jetman.Grounded || jetman.FlyTimeRemaining > 0 || jetman.Treasure is not null)
        {
            // Walk/fly horizontally, blocking only against windows in front of the support.
            var previousX = jetman.X;
            ApplyHorizontalMovement(jetman, dt);
            jetman.X += jetman.Vx * dt;

            jetman.WalkDistance += Math.Abs(jetman.X - previousX);

            if (!jetman.Retiring && jetman.Grounded && jetman.Treasure is null && m_random.NextDouble() < 0.003)
            {
                TrySetIntent(jetman, -jetman.IntentDirection);
            }

            if (!jetman.Retiring && jetman.Grounded && FindSupport(jetman, jetman.X, platforms) is null)
            {
                // At a platform edge, give a short thrust burst instead of just dropping.
                jetman.Vy = 0;
                StartFlying(jetman, EdgeFlyDurationSeconds);
                jetman.Grounded = false;
            }
        }

        if (!jetman.Grounded)
        {
            if (jetman.FlyTimeRemaining > 0)
            {
                // Short bursts of upward thrust for the JetPac-style spaceman.
                jetman.FlyTimeRemaining = Math.Max(0, jetman.FlyTimeRemaining - dt);
                jetman.Vy = Math.Max(MaxUpwardSpeed, jetman.Vy - FlyThrust * dt);
            }

            jetman.FlyAnimationTime += dt;
            jetman.Vy += Gravity * dt;
            jetman.Y += jetman.Vy * dt;

            var landed = jetman.Retiring
                ? null
                : FindLandingPlatform(jetman, platforms, previousBottom, jetman.Y + JetmanHeight);
            if (jetman.Vy >= 0 && landed is not null)
            {
                jetman.Y = landed.Value.Y - JetmanHeight;
                jetman.Vy = 0;
                jetman.Grounded = true;
                jetman.FlyTimeRemaining = 0;
                if (jetman.Treasure is null)
                {
                    ChooseTreasure(jetman);
                }
                if (m_random.NextDouble() < 0.5)
                {
                    TrySetIntent(jetman, -jetman.IntentDirection);
                }
            }
        }

        if (jetman.Treasure is not null)
        {
            if (jetman.X + JetmanWidth < 0)
            {
                jetman.X = Bounds.Width;
            }
            else if (jetman.X > Bounds.Width)
            {
                jetman.X = -JetmanWidth;
            }
        }
        else if (jetman.X < 0)
        {
            jetman.X = 0;
            jetman.Vx = Math.Abs(jetman.Vx);
            SetIntentNow(jetman, 1);
        }
        else if (jetman.X + JetmanWidth > Bounds.Width)
        {
            jetman.X = Math.Max(0, Bounds.Width - JetmanWidth);
            jetman.Vx = -Math.Abs(jetman.Vx);
            SetIntentNow(jetman, -1);
        }

        if (jetman.Y > Bounds.Height)
        {
            if (jetman.Retiring)
            {
                m_jetmen.Remove(jetman);
                return;
            }

            // Recycle jetmen that fall past the bottom of the primary monitor.
            if (!TryPickTopSpawnX(JetmanWidth, m_lastKnownPlatforms, out var respawnX))
            {
                return;
            }

            jetman.X = respawnX;
            jetman.Y = -JetmanHeight;
            jetman.Vy = 0;
            jetman.Grounded = false;
            jetman.WalkDistance = 0;
            jetman.FlyTimeRemaining = 0;
            jetman.FlyAnimationTime = 0;
            jetman.Treasure = null;
            jetman.Vx = 0;
            jetman.IntentDirection = m_random.Next(0, 2) == 0 ? -1 : 1;
            jetman.DirectionCooldown = 0;
        }

        if (jetman.Y < 0 && jetman.Vy < 0)
        {
            jetman.Y = 0;
            jetman.Vy = Math.Max(0, jetman.Vy);
        }

        if (!jetman.Retiring)
        {
            var collectedTreasure = FindCollectedTreasure(jetman);
            if (collectedTreasure is not null)
            {
                CollectTreasure(collectedTreasure, now);
                ChooseTreasure(jetman);
            }
        }
    }

    private IReadOnlyList<Platform> m_lastKnownPlatforms = Array.Empty<Platform>();

    private void InitializeTreasures(double now)
    {
        m_treasures.Clear();
        ReconcileTreasureCount(now);
        m_treasuresInitialized = true;
    }

    private void ReconcileTreasureCount(double now)
    {
        TrimExcessJetmenAndTreasures();

        var spawnAt = Math.Max(now, m_treasures.Count == 0 ? now : m_treasures.Max(t => t.SpawnAt));
        while (m_treasures.Count < m_jetmanLimit)
        {
            spawnAt += RandomSpawnDelay();
            m_treasures.Add(new Treasure
            {
                Active = false,
                SpawnAt = spawnAt
            });
        }
    }

    private void TrimExcessJetmenAndTreasures()
    {
        var activeJetmen = m_jetmen.Count(j => !j.Retiring);
        while (activeJetmen > m_jetmanLimit)
        {
            RetireJetman(m_jetmen.Last(j => !j.Retiring));
            activeJetmen--;
        }

        while (m_treasures.Count > m_jetmanLimit)
        {
            var treasure = m_treasures.LastOrDefault(t => !t.Active) ?? m_treasures[^1];
            ClearTreasureAssignments(treasure);
            m_treasures.Remove(treasure);
        }
    }

    private void SpawnNextJetman(double now)
    {
        if (!m_jetmanSpawningStarted)
        {
            m_jetmanSpawningStarted = true;
            m_nextJetmanSpawnAt = now + RandomSpawnDelay();
        }

        if (m_jetmen.Count(j => !j.Retiring) >= m_jetmanLimit || now < m_nextJetmanSpawnAt)
        {
            return;
        }

        if (!TryPickTopSpawnX(JetmanWidth, m_lastKnownPlatforms, out var spawnX))
        {
            return;
        }

        var jetman = new Jetman
        {
            X = spawnX,
            Y = -JetmanHeight,
            IntentDirection = m_random.Next(0, 2) == 0 ? -1 : 1
        };
        ChooseTreasure(jetman);
        m_jetmen.Add(jetman);
        m_nextJetmanSpawnAt = now + RandomSpawnDelay();
    }

    private void StepTreasure(Treasure treasure, double dt, double now, IReadOnlyList<Platform> platforms)
    {
        if (!treasure.Active)
        {
            if (now >= treasure.SpawnAt)
            {
                TrySpawnTreasure(treasure, platforms);
            }

            return;
        }

        var previousBottom = treasure.Y + TreasureHeight;
        var support = FindTreasureSupport(treasure, platforms);
        if (support is not null && treasure.Vy >= 0)
        {
            treasure.Y = support.Value.Y - TreasureHeight;
            treasure.Vy = 0;
            treasure.Grounded = true;
        }
        else
        {
            treasure.Grounded = false;
            treasure.Vy += Gravity * dt;
            treasure.Y += treasure.Vy * dt;

            var landed = FindTreasureLandingPlatform(treasure, platforms, previousBottom, treasure.Y + TreasureHeight);
            if (treasure.Vy >= 0 && landed is not null)
            {
                treasure.Y = landed.Value.Y - TreasureHeight;
                treasure.Vy = 0;
                treasure.Grounded = true;
            }
        }

        if (treasure.Y > Bounds.Height)
        {
            ScheduleTreasureRespawn(treasure, now);
        }
    }

    private Platform? FindTreasureSupport(Treasure treasure, IReadOnlyList<Platform> platforms)
    {
        var footX = treasure.X + TreasureWidth / 2;
        var footY = treasure.Y + TreasureHeight;

        return platforms
            .Where(s => Math.Abs(s.Y - footY) < 3 && footX >= s.Left && footX <= s.Right)
            .OrderBy(s => s.ZOrder)
            .Select(s => (Platform?)s)
            .FirstOrDefault();
    }

    private Platform? FindTreasureLandingPlatform(Treasure treasure, IReadOnlyList<Platform> platforms, double previousBottom, double currentBottom)
    {
        var centerX = treasure.X + TreasureWidth / 2;

        return platforms
            .Where(s => previousBottom <= s.Y && currentBottom >= s.Y && centerX >= s.Left && centerX <= s.Right)
            .OrderBy(s => s.Y)
            .Select(s => (Platform?)s)
            .FirstOrDefault();
    }

    private void TrySpawnTreasure(Treasure treasure, IReadOnlyList<Platform> platforms)
    {
        var candidates = platforms
            .Where(s => s.Y >= MinTreasureY)
            .Select(s => (Platform: s, Ranges: GetTopClearRanges(s.Left, s.Right - TreasureWidth, TreasureWidth, platforms)))
            .Where(c => c.Ranges.Count > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var candidate = candidates[m_random.Next(candidates.Count)];
        var range = candidate.Ranges[m_random.Next(candidate.Ranges.Count)];
        treasure.SpriteIndex = m_random.Next(TreasureFrameCount);
        treasure.TintIndex = PickTreasureTint(treasure.SpriteIndex);
        treasure.X = range.Left + m_random.NextDouble() * Math.Max(0, range.Right - range.Left);
        treasure.Y = -TreasureHeight;
        treasure.Vy = 0;
        treasure.Active = true;
        treasure.Grounded = false;
    }

    private void ScheduleTreasureRespawn(Treasure treasure, double now)
    {
        treasure.Active = false;
        treasure.Grounded = false;
        treasure.Vy = 0;
        treasure.Y = -TreasureHeight;
        treasure.SpawnAt = now + RandomSpawnDelay();
    }

    private int PickTreasureTint(int spriteIndex)
    {
        return spriteIndex switch
        {
            1 => GreenTintIndex,
            2 => YellowTintIndex,
            _ => m_random.Next(RedTintIndex, CyanTintIndex + 1)
        };
    }

    private double RandomSpawnDelay()
    {
        return MinSpawnDelaySeconds + m_random.NextDouble() * (MaxSpawnDelaySeconds - MinSpawnDelaySeconds);
    }

    private bool TryPickTopSpawnX(double width, IReadOnlyList<Platform> platforms, out double x)
    {
        var ranges = GetTopClearRanges(0, Bounds.Width - width, width, platforms);
        if (ranges.Count == 0)
        {
            x = 0;
            return false;
        }

        x = PickRangeX(ranges);
        return true;
    }

    private List<(double Left, double Right)> GetTopClearRanges(double left, double right, double width, IReadOnlyList<Platform> platforms)
    {
        if (right < left)
        {
            return new List<(double Left, double Right)>();
        }

        var blocked = platforms
            .Where(s => s.Y < TopSpawnClearance)
            .Select(s => (Left: Math.Max(0, s.Left - width), Right: Math.Min(Bounds.Width, s.Right)))
            .Where(s => s.Right > s.Left)
            .OrderBy(s => s.Left)
            .ToList();

        var ranges = new List<(double Left, double Right)>
        {
            (Math.Max(0, left), Math.Min(Math.Max(0, Bounds.Width - width), right))
        };

        foreach (var block in blocked)
        {
            for (var i = ranges.Count - 1; i >= 0; i--)
            {
                var range = ranges[i];
                var coverLeft = Math.Max(range.Left, block.Left);
                var coverRight = Math.Min(range.Right, block.Right);
                if (coverLeft >= coverRight)
                {
                    continue;
                }

                ranges.RemoveAt(i);
                if (range.Left < coverLeft)
                {
                    ranges.Add((range.Left, coverLeft));
                }

                if (coverRight < range.Right)
                {
                    ranges.Add((coverRight, range.Right));
                }
            }
        }

        return ranges.Where(r => r.Right >= r.Left).ToList();
    }

    private double PickRangeX(IReadOnlyList<(double Left, double Right)> ranges)
    {
        var total = ranges.Sum(r => r.Right - r.Left);
        var pick = m_random.NextDouble() * total;
        foreach (var range in ranges)
        {
            var length = range.Right - range.Left;
            if (pick <= length)
            {
                return range.Left + pick;
            }

            pick -= length;
        }

        return ranges[^1].Left;
    }

    private void UpdateTreasure(Jetman jetman)
    {
        if (jetman.Treasure is null)
        {
            ChooseTreasure(jetman);
            return;
        }

        if (!jetman.Treasure.Active || jetman.Treasure.Y > Bounds.Height)
        {
            jetman.Treasure = null;
            ChooseTreasure(jetman);
        }
    }

    private Treasure FindCollectedTreasure(Jetman jetman)
    {
        return m_treasures
            .Where(t => t.Active)
            .Where(t => IsAtTreasure(jetman, t) ||
                        RectsOverlap(
                            jetman.X,
                            jetman.Y,
                            JetmanWidth,
                            JetmanHeight,
                            t.X,
                            t.Y + TreasurePickupTopInset,
                            TreasureWidth,
                            TreasurePickupHeight))
            .OrderBy(t => DistanceToTreasure(jetman, t))
            .FirstOrDefault();
    }

    private void CollectTreasure(Treasure treasure, double now)
    {
        ScheduleTreasureRespawn(treasure, now);
        ClearTreasureAssignments(treasure);
    }

    private void ClearTreasureAssignments(Treasure treasure)
    {
        foreach (var jetman in m_jetmen.Where(p => ReferenceEquals(p.Treasure, treasure)))
        {
            jetman.Treasure = null;
        }
    }

    private void RetireJetman(Jetman jetman)
    {
        jetman.Retiring = true;
        jetman.Treasure = null;
        jetman.Grounded = false;
        jetman.FlyTimeRemaining = 0;
        jetman.Vy = Math.Max(jetman.Vy, 0);
    }

    private bool IsAtTreasure(Jetman jetman, Treasure treasure)
    {
        var jetmanCenterX = jetman.X + JetmanWidth / 2;
        var jetmanCenterY = jetman.Y + JetmanHeight / 2;
        var treasureCenterX = treasure.X + TreasureWidth / 2;
        var treasureCenterY = treasure.Y + TreasurePickupTopInset + TreasurePickupHeight / 2;
        var dx = SignedWrappedDelta(treasureCenterX, jetmanCenterX);
        var dy = jetmanCenterY - treasureCenterY;

        return Math.Sqrt(dx * dx + dy * dy) <= TreasureArrivalDistance;
    }

    private void ChooseTreasure(Jetman jetman)
    {
        var assignedTreasures = m_jetmen
            .Where(p => p != jetman && p.Treasure is not null)
            .Select(p => p.Treasure)
            .ToHashSet();

        var candidates = m_treasures
            .Where(t => t.Active && t.Y >= MinTreasureY && t.Y <= Bounds.Height)
            .Where(t => !assignedTreasures.Contains(t))
            .ToList();

        if (candidates.Count == 0)
        {
            jetman.Treasure = null;
            return;
        }

        jetman.Treasure = candidates
            .OrderBy(t => DistanceToTreasure(jetman, t))
            .First();
    }

    private double DistanceToTreasure(Jetman jetman, Treasure treasure)
    {
        var jetmanCenterX = jetman.X + JetmanWidth / 2;
        var jetmanCenterY = jetman.Y + JetmanHeight / 2;
        var treasureCenterX = treasure.X + TreasureWidth / 2;
        var treasureCenterY = treasure.Y + TreasureHeight / 2;
        var dx = Math.Abs(SignedWrappedDelta(jetmanCenterX, treasureCenterX));
        var dy = Math.Abs(jetmanCenterY - treasureCenterY);
        return dx + dy;
    }

    private int GetWalkFrame(Jetman jetman)
    {
        var cycleDistance = JetmanWidth;
        var frameDistance = cycleDistance / WalkFrameCount;
        return (int)Math.Floor(jetman.WalkDistance / frameDistance) % WalkFrameCount;
    }

    private void ApplyHorizontalMovement(Jetman jetman, double dt)
    {
        var desiredVx = jetman.IntentDirection * WalkSpeed;
        jetman.Vx = MoveTowards(jetman.Vx, desiredVx, HorizontalAcceleration * dt);

        if (jetman.IntentDirection == 0)
        {
            var friction = jetman.Grounded ? GroundFriction : AirFriction;
            jetman.Vx *= Math.Exp(-friction * dt);
        }
    }

    private static double MoveTowards(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maxDelta;
    }

    private void SteerToTreasure(Jetman jetman, Platform? currentSupport)
    {
        if (jetman.Treasure is null)
        {
            return;
        }

        var treasureX = jetman.Treasure.X + (TreasureWidth - JetmanWidth) / 2;
        var steerX = treasureX;
        if (jetman.Grounded &&
            currentSupport is not null &&
            jetman.Treasure.Y + TreasureHeight > currentSupport.Value.Y + DescendThreshold)
        {
            steerX = PickDescentEdgeX(treasureX, currentSupport.Value);
        }

        var dx = SignedWrappedDelta(jetman.X, steerX);
        if (Math.Abs(dx) > TreasureSteerDeadZone)
        {
            TrySetIntent(jetman, dx > 0 ? 1 : -1);
        }

        var treasureY = jetman.Treasure.Y + TreasureHeight - JetmanHeight;
        var treasureAbove = treasureY < jetman.Y - ClimbThreshold;
        var fallingNearTreasure = !jetman.Grounded &&
            jetman.Vy > MaxFallSpeedNearTreasure &&
            jetman.Y < treasureY &&
            treasureY - jetman.Y < SlowFallDistance;

        if (treasureAbove || fallingNearTreasure)
        {
            StartFlying(jetman, Math.Min(FlyDurationSeconds, 0.35));
            jetman.Grounded = false;
        }
    }

    private double PickDescentEdgeX(double treasureX, Platform support)
    {
        var leftExitX = support.Left - JetmanWidth;
        var rightExitX = support.Right;

        if (treasureX < support.Left)
        {
            return leftExitX;
        }

        if (treasureX > support.Right - JetmanWidth)
        {
            return rightExitX;
        }

        return treasureX - support.Left < support.Right - treasureX
            ? leftExitX
            : rightExitX;
    }

    private double SignedWrappedDelta(double fromX, double toX)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return toX - fromX;
        }

        var direct = toX - fromX;
        if (direct > width / 2)
        {
            return direct - width;
        }

        if (direct < -width / 2)
        {
            return direct + width;
        }

        return direct;
    }

    private void StartFlying(Jetman jetman, double durationSeconds)
    {
        var wasFlying = jetman.FlyTimeRemaining > 0;
        var wasGrounded = jetman.Grounded;
        jetman.FlyTimeRemaining = Math.Max(jetman.FlyTimeRemaining, durationSeconds);
        if (!wasFlying)
        {
            jetman.FlyAnimationTime = 0;
            if (wasGrounded)
            {
                jetman.SmokeAnchorX = jetman.X + JetmanWidth / 2;
                jetman.SmokeAnchorY = jetman.Y + JetmanHeight;
                jetman.SmokeAnimationTime = 0;
            }
        }
    }
    
    private static void TrySetIntent(Jetman jetman, int direction)
    {
        direction = Math.Sign(direction);
        if (direction == 0 || direction == jetman.IntentDirection)
        {
            return;
        }

        if (jetman.DirectionCooldown > 0)
        {
            return;
        }

        SetIntentNow(jetman, direction);
    }

    private static void SetIntentNow(Jetman jetman, int direction)
    {
        jetman.IntentDirection = Math.Sign(direction) == 0 ? jetman.IntentDirection : Math.Sign(direction);
        jetman.DirectionCooldown = DirectionChangeCooldownSeconds;
    }

    private Platform? FindSupport(Jetman jetman, double x, IReadOnlyList<Platform> platforms)
    {
        var footX = x + JetmanWidth / 2;
        var footY = jetman.Y + JetmanHeight;

        // Prefer the topmost matching platform when windows overlap.
        return platforms
            .Where(s => Math.Abs(s.Y - footY) < 3 && footX >= s.Left && footX <= s.Right)
            .OrderBy(s => s.ZOrder)
            .Select(s => (Platform?)s)
            .FirstOrDefault();
    }

    private Platform? FindLandingPlatform(Jetman jetman, IReadOnlyList<Platform> platforms, double previousBottom, double currentBottom)
    {
        var centerX = jetman.X + JetmanWidth / 2;

        // Swept vertical collision prevents tunneling through thin window tops.
        return platforms
            .Where(s => previousBottom <= s.Y && currentBottom >= s.Y && centerX >= s.Left && centerX <= s.Right)
            .OrderBy(s => s.Y)
            .Select(s => (Platform?)s)
            .FirstOrDefault();
    }
    
    private static bool RectsOverlap(
        double ax,
        double ay,
        double aw,
        double ah,
        double bx,
        double by,
        double bw,
        double bh)
    {
        return ax < bx + bw &&
               ax + aw > bx &&
               ay < by + bh &&
               ay + ah > by;
    }

    /// <summary>
    /// Stores the simulation state for one spaceman.
    /// </summary>
    /// <remarks>
    /// Jetmen need independent motion, animation, and treasure intent so several actors can wander the desktop at once.
    /// </remarks>
    private sealed class Jetman
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double WalkDistance { get; set; }
        public double FlyTimeRemaining { get; set; }
        public double FlyAnimationTime { get; set; }
        public double SmokeAnchorX { get; set; }
        public double SmokeAnchorY { get; set; }
        public double SmokeAnimationTime { get; set; } = SmokeAnimationFrameCount / SimulationFramesPerSecond;
        public double DirectionCooldown { get; set; }
        public Treasure Treasure { get; set; }
        public int IntentDirection { get; set; } = 1;
        public bool Grounded { get; set; }
        public bool Retiring { get; set; }
    }

    /// <summary>
    /// Stores the simulation state for one collectible treasure.
    /// </summary>
    /// <remarks>
    /// Treasures fall, land, respawn, and carry sprite tint choices independently from the jetmen chasing them.
    /// </remarks>
    private sealed class Treasure
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vy { get; set; }
        public int SpriteIndex { get; set; }
        public int TintIndex { get; set; }
        public bool Active { get; set; }
        public double SpawnAt { get; set; }
        public bool Grounded { get; set; }
    }
}
