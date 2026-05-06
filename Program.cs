using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskPet;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}

public sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new PetWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public sealed class PetWindow : Window
{
    private readonly PetView _view = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _totalClock = Stopwatch.StartNew();
    private PixelRect _activeScreenBounds;
    private System.Threading.Timer? _timer;
    private int _tickQueued;

    public PetWindow()
    {
        // Transparent, borderless overlay that sits above the primary desktop.
        Content = _view;
        Background = Brushes.Transparent;
        CanResize = false;
        ExtendClientAreaToDecorationsHint = false;
        ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        ShowInTaskbar = true;
        SystemDecorations = SystemDecorations.None;
        Title = "DeskPet";
        Topmost = true;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = new Cursor(StandardCursorType.None);

        Opened += (_, _) =>
        {
            FitVirtualDesktop();
            WindowsInterop.MakeClickThrough(this);
            _view.Reset(Bounds.Width);
            _clock.Restart();

            // Drive the pet simulation from a background timer, then marshal back to the UI thread.
            _timer = new System.Threading.Timer(_ =>
            {
                if (Interlocked.Exchange(ref _tickQueued, 1) == 0)
                {
                    Dispatcher.UIThread.Post(Tick);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
        };

        Closed += (_, _) => _timer?.Dispose();
    }

    private void Tick()
    {
        try
        {
            Interlocked.Exchange(ref _tickQueued, 0);

            // Clamp large pauses so debugging/breakpoints do not launch pets through surfaces.
            var dt = Math.Min(_clock.Elapsed.TotalSeconds, 0.05);
            _clock.Restart();
            _view.Step(dt, _totalClock.Elapsed.TotalSeconds, WindowsInterop.GetWindowSurfaces(_activeScreenBounds, WindowsInterop.GetHandle(this)));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private void FitVirtualDesktop()
    {
        // Prototype scope: only use the primary monitor for now.
        var primary = Screens.Primary ?? Screens.ScreenFromWindow(this);
        if (primary is null)
        {
            Width = 1280;
            Height = 720;
            return;
        }

        _activeScreenBounds = primary.Bounds;
        Position = new PixelPoint(_activeScreenBounds.X, _activeScreenBounds.Y);
        Width = _activeScreenBounds.Width;
        Height = _activeScreenBounds.Height;
        _view.Width = _activeScreenBounds.Width;
        _view.Height = _activeScreenBounds.Height;
    }
}

public sealed class PetView : Control
{
    private const int PetCount = 3;
    private const int WalkFrameCount = 4;
    private const int FlyFrameCount = 4;
    private const int TargetCount = PetCount;
    private const int TargetFrameCount = 5;
    private const double Gravity = 100;
    private const double FlyChancePerSecond = 0.08;
    private const double FlyDurationSeconds = 1.0;
    private const double EdgeFlyDurationSeconds = 0.5;
    private const double WallAvoidanceFlyDurationSeconds = 1.0;
    private const double FlyThrust = 200;
    private const double MaxUpwardSpeed = -100;
    private const double WalkSpeed = 190;
    private const double HorizontalAcceleration = 280;
    private const double GroundFriction = 7;
    private const double AirFriction = 1.5;
    private const double MinTargetY = 100;
    private const double TopSpawnClearance = 100;
    private const double TargetArrivalDistance = 18;
    private const double TargetSteerDeadZone = 12;
    private const double ClimbThreshold = 18;
    private const double DescendThreshold = 10;
    private const double SlowFallDistance = 80;
    private const double MaxFallSpeedNearTarget = 80;
    private const double DirectionChangeCooldownSeconds = 1;
    private const double MinSpawnDelaySeconds = 1;
    private const double MaxSpawnDelaySeconds = 2;
    private static readonly bool IgnoreWindowSideCollisions = true;
    private readonly Random _random = new();
    private readonly List<Pet> _pets = new();
    private readonly List<Target> _targets = new();
    private bool _targetsInitialized;
    private bool _petSpawningStarted;
    private double _nextPetSpawnAt;

    // Four left-facing walk frames in one horizontal strip.
    private readonly Bitmap _walkSheet = new(AssetLoader.Open(new Uri("avares://DeskPet/Assets/walk.png")));
    private readonly Bitmap _flySheet = new(AssetLoader.Open(new Uri("avares://DeskPet/Assets/fly.png")));
    private readonly Bitmap _targetSheet = new(AssetLoader.Open(new Uri("avares://DeskPet/Assets/targets.png")));
    private double PetWidth => _walkSheet.Size.Width / WalkFrameCount;
    private double PetHeight => _walkSheet.Size.Height;
    private double TargetWidth => _targetSheet.Size.Width / TargetFrameCount;
    private double TargetHeight => _targetSheet.Size.Height;

    public void Reset(double screenWidth)
    {
        _pets.Clear();
        _targets.Clear();
        _targetsInitialized = false;
        _petSpawningStarted = false;
        _nextPetSpawnAt = 0;
        InvalidateVisual();
    }

    public void Step(double dt, double now, IReadOnlyList<Surface> surfaces)
    {
        _lastKnownSurfaces = surfaces;

        if (!_targetsInitialized)
        {
            InitializeTargets(now);
        }

        foreach (var target in _targets)
        {
            StepTarget(target, dt, now, surfaces);
        }

        if (_targets.Count > 0 && _targets.All(t => t.Active && t.Grounded))
        {
            SpawnNextPet(now);
        }

        foreach (var pet in _pets)
        {
            StepPet(pet, dt, now, surfaces);
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        foreach (var target in _targets)
        {
            DrawTarget(context, target);
        }

        foreach (var pet in _pets)
        {
            DrawPet(context, pet);
        }
    }

    private void DrawTarget(DrawingContext context, Target target)
    {
        if (!target.Active)
        {
            return;
        }

        var frameWidth = TargetWidth;
        var source = new Rect(target.SpriteIndex * frameWidth, 0, frameWidth, TargetHeight);
        var dest = new Rect(target.X, target.Y, TargetWidth, TargetHeight);
        context.DrawImage(_targetSheet, source, dest);
    }

    private void DrawPet(DrawingContext context, Pet pet)
    {
        var sheet = pet.FlyTimeRemaining > 0 ? _flySheet : _walkSheet;
        var frameCount = pet.FlyTimeRemaining > 0 ? FlyFrameCount : WalkFrameCount;
        var frameWidth = PetWidth;
        var frameHeight = PetHeight;

        // One complete animation cycle should cover one frame-width of travel.
        var frame = pet.FlyTimeRemaining > 0
            ? (int)Math.Floor(pet.FlyAnimationTime * 12) % frameCount
            : pet.Grounded
                ? (int)Math.Floor(pet.WalkDistance / (PetWidth / WalkFrameCount)) % frameCount
                : 0;
        var source = new Rect(frame * frameWidth, 0, frameWidth, frameHeight);
        var dest = new Rect(pet.X, pet.Y, PetWidth, PetHeight);

        if ((Math.Abs(pet.Vx) > 1 ? pet.Vx : pet.IntentDirection) > 0)
        {
            // The source art faces left, so mirror it for rightward walking.
            using (context.PushTransform(Matrix.CreateScale(-1, 1)))
            {
                context.DrawImage(sheet, source, new Rect(-(pet.X + PetWidth), pet.Y, PetWidth, PetHeight));
            }
        }
        else
        {
            context.DrawImage(sheet, source, dest);
        }
    }

    private void StepPet(Pet pet, double dt, double now, IReadOnlyList<Surface> surfaces)
    {
        var previousBottom = pet.Y + PetHeight;
        pet.DirectionCooldown = Math.Max(0, pet.DirectionCooldown - dt);
        var currentSupport = FindSupport(pet, pet.X, surfaces);
        UpdateTarget(pet, now);
        SteerToTarget(pet, currentSupport);

        if (pet.FlyTimeRemaining <= 0 && _random.NextDouble() < FlyChancePerSecond * dt)
        {
            StartFlying(pet, FlyDurationSeconds);
            pet.Grounded = false;
        }

        if (pet.Grounded || pet.FlyTimeRemaining > 0 || pet.Target is not null)
        {
            // Walk/fly horizontally, blocking only against windows in front of the support.
            var previousX = pet.X;
            var support = pet.Grounded ? FindSupport(pet, previousX, surfaces) : null;
            ApplyHorizontalMovement(pet, dt);
            pet.X += pet.Vx * dt;

            if (TryHitWall(pet, previousX, support, surfaces))
            {
                pet.WalkDistance += Math.Abs(pet.X - previousX);
            }
            else
            {
                pet.WalkDistance += Math.Abs(pet.X - previousX);

                if (pet.Grounded && pet.Target is null && _random.NextDouble() < 0.003)
                {
                    TrySetIntent(pet, -pet.IntentDirection);
                }

                if (pet.Grounded && FindSupport(pet, pet.X, surfaces) is null)
                {
                    // At a platform edge, give a short thrust burst instead of just dropping.
                    pet.Grounded = false;
                    pet.Vy = 0;
                    StartFlying(pet, EdgeFlyDurationSeconds);
                }
            }
        }

        if (!pet.Grounded)
        {
            if (pet.FlyTimeRemaining > 0)
            {
                // Short bursts of upward thrust for the JetPac-style spaceman.
                pet.FlyTimeRemaining = Math.Max(0, pet.FlyTimeRemaining - dt);
                pet.FlyAnimationTime += dt;
                pet.Vy = Math.Max(MaxUpwardSpeed, pet.Vy - FlyThrust * dt);
            }

            pet.Vy += Gravity * dt;
            pet.Y += pet.Vy * dt;

            var landed = FindLandingSurface(pet, surfaces, previousBottom, pet.Y + PetHeight);
            if (pet.Vy >= 0 && landed is not null)
            {
                pet.Y = landed.Value.Y - PetHeight;
                pet.Vy = 0;
                pet.Grounded = true;
                pet.FlyTimeRemaining = 0;
                if (pet.Target is null)
                {
                    ChooseTarget(pet, now);
                }
                if (_random.NextDouble() < 0.5)
                {
                    TrySetIntent(pet, -pet.IntentDirection);
                }
            }
        }

        if (pet.Target is not null)
        {
            if (pet.X + PetWidth < 0)
            {
                pet.X = Bounds.Width;
            }
            else if (pet.X > Bounds.Width)
            {
                pet.X = -PetWidth;
            }
        }
        else if (pet.X < 0)
        {
            pet.X = 0;
            pet.Vx = Math.Abs(pet.Vx);
            SetIntentNow(pet, 1);
        }
        else if (pet.X + PetWidth > Bounds.Width)
        {
            pet.X = Math.Max(0, Bounds.Width - PetWidth);
            pet.Vx = -Math.Abs(pet.Vx);
            SetIntentNow(pet, -1);
        }

        if (pet.Y > Bounds.Height)
        {
            // Recycle pets that fall past the bottom of the primary monitor.
            if (!TryPickTopSpawnX(PetWidth, _lastKnownSurfaces, out var respawnX))
            {
                return;
            }

            pet.X = respawnX;
            pet.Y = -PetHeight;
            pet.Vy = 0;
            pet.Grounded = false;
            pet.WalkDistance = 0;
            pet.FlyTimeRemaining = 0;
            pet.FlyAnimationTime = 0;
            pet.Target = null;
            pet.TargetSelectedAt = now;
            pet.Vx = 0;
            pet.IntentDirection = _random.Next(0, 2) == 0 ? -1 : 1;
            pet.DirectionCooldown = 0;
        }

        if (pet.Y < 0)
        {
            pet.Y = 0;
            pet.Vy = Math.Max(0, pet.Vy);
        }

        if (pet.Target is not null &&
            (IsAtTarget(pet) ||
             RectsOverlap(
                pet.X,
                pet.Y,
                PetWidth,
                PetHeight,
                pet.Target.X,
                pet.Target.Y,
                TargetWidth,
                TargetHeight)))
        {
            ScheduleTargetRespawn(pet.Target, now);
            pet.Target = null;
            ChooseTarget(pet, now);
        }
    }

    private IReadOnlyList<Surface> _lastKnownSurfaces = Array.Empty<Surface>();

    private void InitializeTargets(double now)
    {
        _targets.Clear();
        var spawnAt = now;
        for (var i = 0; i < TargetCount; i++)
        {
            spawnAt += RandomSpawnDelay();
            _targets.Add(new Target
            {
                Active = false,
                SpawnAt = spawnAt
            });
        }

        _targetsInitialized = true;
    }

    private void SpawnNextPet(double now)
    {
        if (!_petSpawningStarted)
        {
            _petSpawningStarted = true;
            _nextPetSpawnAt = now + RandomSpawnDelay();
        }

        if (_pets.Count >= PetCount || now < _nextPetSpawnAt)
        {
            return;
        }

        if (!TryPickTopSpawnX(PetWidth, _lastKnownSurfaces, out var spawnX))
        {
            return;
        }

        var pet = new Pet
        {
            X = spawnX,
            Y = -PetHeight,
            IntentDirection = _random.Next(0, 2) == 0 ? -1 : 1,
            TargetSelectedAt = now
        };
        ChooseTarget(pet, now);
        _pets.Add(pet);
        _nextPetSpawnAt = now + RandomSpawnDelay();
    }

    private void StepTarget(Target target, double dt, double now, IReadOnlyList<Surface> surfaces)
    {
        if (!target.Active)
        {
            if (now >= target.SpawnAt)
            {
                TrySpawnTarget(target, surfaces);
            }

            return;
        }

        var previousBottom = target.Y + TargetHeight;
        var support = FindTargetSupport(target, surfaces);
        if (support is not null && target.Vy >= 0)
        {
            target.Y = support.Value.Y - TargetHeight;
            target.Vy = 0;
            target.Grounded = true;
            target.Support = support;
        }
        else
        {
            target.Grounded = false;
            target.Support = null;
            target.Vy += Gravity * dt;
            target.Y += target.Vy * dt;

            var landed = FindTargetLandingSurface(target, surfaces, previousBottom, target.Y + TargetHeight);
            if (target.Vy >= 0 && landed is not null)
            {
                target.Y = landed.Value.Y - TargetHeight;
                target.Vy = 0;
                target.Grounded = true;
                target.Support = landed;
            }
        }

        if (target.Y > Bounds.Height)
        {
            ScheduleTargetRespawn(target, now);
        }
    }

    private Surface? FindTargetSupport(Target target, IReadOnlyList<Surface> surfaces)
    {
        var footX = target.X + TargetWidth / 2;
        var footY = target.Y + TargetHeight;

        return surfaces
            .Where(s => Math.Abs(s.Y - footY) < 3 && footX >= s.Left && footX <= s.Right)
            .OrderBy(s => s.ZOrder)
            .Select(s => (Surface?)s)
            .FirstOrDefault();
    }

    private Surface? FindTargetLandingSurface(Target target, IReadOnlyList<Surface> surfaces, double previousBottom, double currentBottom)
    {
        var centerX = target.X + TargetWidth / 2;

        return surfaces
            .Where(s => previousBottom <= s.Y && currentBottom >= s.Y && centerX >= s.Left && centerX <= s.Right)
            .OrderBy(s => s.Y)
            .Select(s => (Surface?)s)
            .FirstOrDefault();
    }

    private bool TrySpawnTarget(Target target, IReadOnlyList<Surface> surfaces)
    {
        var candidates = surfaces
            .Where(s => s.Y >= MinTargetY)
            .Select(s => (Surface: s, Ranges: GetTopClearRanges(s.Left, s.Right - TargetWidth, TargetWidth, surfaces)))
            .Where(c => c.Ranges.Count > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return false;
        }

        var candidate = candidates[_random.Next(candidates.Count)];
        var range = candidate.Ranges[_random.Next(candidate.Ranges.Count)];
        target.SpriteIndex = _random.Next(TargetFrameCount);
        target.X = range.Left + _random.NextDouble() * Math.Max(0, range.Right - range.Left);
        target.Y = -TargetHeight;
        target.Vy = 0;
        target.Active = true;
        target.Grounded = false;
        target.Support = null;
        return true;
    }

    private void ScheduleTargetRespawn(Target target, double now)
    {
        target.Active = false;
        target.Grounded = false;
        target.Support = null;
        target.Vy = 0;
        target.Y = -TargetHeight;
        target.SpawnAt = now + RandomSpawnDelay();
    }

    private double RandomSpawnDelay()
    {
        return MinSpawnDelaySeconds + _random.NextDouble() * (MaxSpawnDelaySeconds - MinSpawnDelaySeconds);
    }

    private bool TryPickTopSpawnX(double width, IReadOnlyList<Surface> surfaces, out double x)
    {
        var ranges = GetTopClearRanges(0, Bounds.Width - width, width, surfaces);
        if (ranges.Count == 0)
        {
            x = 0;
            return false;
        }

        x = PickRangeX(ranges);
        return true;
    }

    private List<(double Left, double Right)> GetTopClearRanges(double left, double right, double width, IReadOnlyList<Surface> surfaces)
    {
        if (right < left)
        {
            return new List<(double Left, double Right)>();
        }

        var blocked = surfaces
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
        var pick = _random.NextDouble() * total;
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

    private void UpdateTarget(Pet pet, double now)
    {
        if (pet.Target is null)
        {
            ChooseTarget(pet, now);
            return;
        }

        if (!pet.Target.Active || pet.Target.Y > Bounds.Height)
        {
            pet.Target = null;
            ChooseTarget(pet, now);
        }
    }

    private bool IsAtTarget(Pet pet)
    {
        if (pet.Target is null)
        {
            return false;
        }

        var petCenterX = pet.X + PetWidth / 2;
        var petCenterY = pet.Y + PetHeight / 2;
        var targetCenterX = pet.Target.X + TargetWidth / 2;
        var targetCenterY = pet.Target.Y + TargetHeight / 2;
        var dx = SignedWrappedDelta(targetCenterX, petCenterX);
        var dy = petCenterY - targetCenterY;

        return Math.Sqrt(dx * dx + dy * dy) <= TargetArrivalDistance;
    }

    private void ChooseTarget(Pet pet, double now)
    {
        var assignedTargets = _pets
            .Where(p => p != pet && p.Target is not null)
            .Select(p => p.Target)
            .ToHashSet();

        var candidates = _targets
            .Where(t => t.Active && t.Y >= MinTargetY && t.Y <= Bounds.Height)
            .Where(t => !assignedTargets.Contains(t))
            .ToList();

        if (candidates.Count == 0)
        {
            pet.Target = null;
            pet.TargetSelectedAt = now;
            return;
        }

        pet.Target = candidates
            .OrderBy(t => DistanceToTarget(pet, t))
            .First();
        pet.TargetSelectedAt = now;
    }

    private double DistanceToTarget(Pet pet, Target target)
    {
        var petCenterX = pet.X + PetWidth / 2;
        var petCenterY = pet.Y + PetHeight / 2;
        var targetCenterX = target.X + TargetWidth / 2;
        var targetCenterY = target.Y + TargetHeight / 2;
        var dx = Math.Abs(SignedWrappedDelta(petCenterX, targetCenterX));
        var dy = Math.Abs(petCenterY - targetCenterY);
        return dx + dy;
    }

    private static void ApplyHorizontalMovement(Pet pet, double dt)
    {
        var desiredVx = pet.IntentDirection * WalkSpeed;
        pet.Vx = MoveTowards(pet.Vx, desiredVx, HorizontalAcceleration * dt);

        var friction = pet.Grounded ? GroundFriction : AirFriction;
        pet.Vx *= Math.Max(0, 1 - friction * dt);
    }

    private static double MoveTowards(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maxDelta;
    }

    private void SteerToTarget(Pet pet, Surface? currentSupport)
    {
        if (pet.Target is null)
        {
            return;
        }

        var targetX = pet.Target.X + (TargetWidth - PetWidth) / 2;
        var steerX = targetX;
        if (pet.Grounded &&
            currentSupport is not null &&
            pet.Target.Y + TargetHeight > currentSupport.Value.Y + DescendThreshold)
        {
            steerX = PickDescentEdgeX(targetX, currentSupport.Value);
        }

        var dx = SignedWrappedDelta(pet.X, steerX);
        if (Math.Abs(dx) > TargetSteerDeadZone)
        {
            TrySetIntent(pet, dx > 0 ? 1 : -1);
        }

        var targetY = pet.Target.Y + TargetHeight - PetHeight;
        var targetAbove = targetY < pet.Y - ClimbThreshold;
        var fallingNearTarget = !pet.Grounded &&
            pet.Vy > MaxFallSpeedNearTarget &&
            pet.Y < targetY &&
            targetY - pet.Y < SlowFallDistance;

        if (targetAbove || fallingNearTarget)
        {
            StartFlying(pet, Math.Min(FlyDurationSeconds, 0.35));
            pet.Grounded = false;
        }
    }

    private double PickDescentEdgeX(double targetX, Surface support)
    {
        var leftExitX = support.Left - PetWidth;
        var rightExitX = support.Right;

        if (targetX < support.Left)
        {
            return leftExitX;
        }

        if (targetX > support.Right - PetWidth)
        {
            return rightExitX;
        }

        return targetX - support.Left < support.Right - targetX
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

    private static void StartFlying(Pet pet, double durationSeconds)
    {
        var wasFlying = pet.FlyTimeRemaining > 0;
        pet.FlyTimeRemaining = Math.Max(pet.FlyTimeRemaining, durationSeconds);
        if (!wasFlying)
        {
            pet.FlyAnimationTime = 0;
        }
    }

    private static bool TrySetDirection(Pet pet, int direction)
    {
        return TrySetIntent(pet, direction);
    }

    private static bool TrySetIntent(Pet pet, int direction)
    {
        direction = Math.Sign(direction);
        if (direction == 0 || direction == pet.IntentDirection)
        {
            return false;
        }

        if (pet.DirectionCooldown > 0)
        {
            return false;
        }

        SetIntentNow(pet, direction);
        return true;
    }

    private static void SetIntentNow(Pet pet, int direction)
    {
        pet.IntentDirection = Math.Sign(direction) == 0 ? pet.IntentDirection : Math.Sign(direction);
        pet.DirectionCooldown = DirectionChangeCooldownSeconds;
    }

    private Surface? FindSupport(Pet pet, double x, IReadOnlyList<Surface> surfaces)
    {
        var footX = x + PetWidth / 2;
        var footY = pet.Y + PetHeight;

        // Prefer the topmost matching surface when windows overlap.
        return surfaces
            .Where(s => Math.Abs(s.Y - footY) < 3 && footX >= s.Left && footX <= s.Right)
            .OrderBy(s => s.ZOrder)
            .Select(s => (Surface?)s)
            .FirstOrDefault();
    }

    private Surface? FindLandingSurface(Pet pet, IReadOnlyList<Surface> surfaces, double previousBottom, double currentBottom)
    {
        var centerX = pet.X + PetWidth / 2;

        // Swept vertical collision prevents tunneling through thin window tops.
        return surfaces
            .Where(s => previousBottom <= s.Y && currentBottom >= s.Y && centerX >= s.Left && centerX <= s.Right)
            .OrderBy(s => s.Y)
            .Select(s => (Surface?)s)
            .FirstOrDefault();
    }

    private bool TryHitWall(Pet pet, double previousX, Surface? support, IReadOnlyList<Surface> surfaces)
    {
        if (IgnoreWindowSideCollisions)
        {
            return false;
        }

        var bodyTop = pet.Y + 2;
        var bodyBottom = pet.Y + PetHeight - 2;

        // Only windows in front of the current support act as vertical walls.
        var blockers = surfaces
            .Where(s => (support is null || s.ZOrder < support.Value.ZOrder) && s.Y < bodyBottom && s.Bottom > bodyTop)
            .ToList();

        if (pet.IntentDirection > 0)
        {
            var previousRight = previousX + PetWidth;
            var currentRight = pet.X + PetWidth;
            var hit = blockers
                .Where(s => previousRight <= s.Left && currentRight >= s.Left)
                .OrderBy(s => s.Left)
                .Select(s => (Surface?)s)
                .FirstOrDefault();

            if (hit is not null)
            {
                if (ShouldFlyOverWall(pet, hit.Value))
                {
                    pet.X = hit.Value.Left - PetWidth;
                    StartFlying(pet, WallAvoidanceFlyDurationSeconds);
                    pet.Grounded = false;
                    pet.Vy = Math.Min(pet.Vy, 0);
                    return true;
                }

                pet.X = hit.Value.Left - PetWidth;
                SetIntentNow(pet, -1);
                pet.Vx = -Math.Abs(pet.Vx) * 0.5;
                return true;
            }
        }
        else
        {
            var hit = blockers
                .Where(s => previousX >= s.Right && pet.X <= s.Right)
                .OrderByDescending(s => s.Right)
                .Select(s => (Surface?)s)
                .FirstOrDefault();

            if (hit is not null)
            {
                if (ShouldFlyOverWall(pet, hit.Value))
                {
                    pet.X = hit.Value.Right;
                    StartFlying(pet, WallAvoidanceFlyDurationSeconds);
                    pet.Grounded = false;
                    pet.Vy = Math.Min(pet.Vy, 0);
                    return true;
                }

                pet.X = hit.Value.Right;
                SetIntentNow(pet, 1);
                pet.Vx = Math.Abs(pet.Vx) * 0.5;
                return true;
            }
        }

        return false;
    }

    private bool ShouldFlyOverWall(Pet pet, Surface wall)
    {
        if (pet.Target is null)
        {
            return false;
        }

        var targetX = pet.Target.X + (TargetWidth - PetWidth) / 2;
        return pet.IntentDirection > 0
            ? targetX > wall.Right
            : targetX + PetWidth < wall.Left;
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

    private sealed class Pet
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double WalkDistance { get; set; }
        public double FlyTimeRemaining { get; set; }
        public double FlyAnimationTime { get; set; }
        public double DirectionCooldown { get; set; }
        public Target? Target { get; set; }
        public double TargetSelectedAt { get; set; }
        public int IntentDirection { get; set; } = 1;
        public bool Grounded { get; set; }
    }

    private sealed class Target
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Vy { get; set; }
        public int SpriteIndex { get; set; }
        public bool Active { get; set; }
        public double SpawnAt { get; set; }
        public bool Grounded { get; set; }
        public Surface? Support { get; set; }
    }
}

public readonly record struct Surface(double Left, double Right, double Y, double Bottom, int ZOrder);

internal static partial class WindowsInterop
{
    public static IReadOnlyList<Surface> GetWindowSurfaces(PixelRect screenBounds, IntPtr self)
    {
        if (!OperatingSystem.IsWindows() || screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return Array.Empty<Surface>();
        }

        var surfaces = new List<Surface>();
        var occluders = new List<NativeRect>();
        var zOrder = 0;

        // EnumWindows returns top-level windows from front to back.
        EnumWindows((hwnd, _) =>
        {
            if (hwnd == self || !IsUsableWindow(hwnd))
            {
                return true;
            }

            if (GetWindowRect(hwnd, out var rect))
            {
                var currentZOrder = zOrder++;
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width <= 80 ||
                    height <= 40 ||
                    rect.Bottom <= screenBounds.Y ||
                    rect.Top >= screenBounds.Bottom ||
                    rect.Right <= screenBounds.X ||
                    rect.Left >= screenBounds.Right)
                {
                    return true;
                }

                if (rect.Top >= screenBounds.Y && rect.Top <= screenBounds.Bottom)
                {
                    // Add only the top-edge portions not covered by higher Z-order windows.
                    AddVisibleTopSegments(surfaces, rect, occluders, screenBounds, currentZOrder);
                }

                occluders.Add(rect);
            }

            return true;
        }, IntPtr.Zero);

        return surfaces;
    }

    private static bool IsUsableWindow(IntPtr hwnd)
    {
        // Filter out minimized, invisible, tool, and cloaked shell windows.
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || GetWindowTextLength(hwnd) == 0)
        {
            return false;
        }

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        if (DwmGetWindowAttribute(hwnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        return true;
    }

    private static void AddVisibleTopSegments(
        List<Surface> surfaces,
        NativeRect rect,
        IReadOnlyList<NativeRect> occluders,
        PixelRect screenBounds,
        int zOrder)
    {
        // Split the top edge around windows that are in front of this one.
        var segments = new List<(int Left, int Right)>
        {
            (Math.Max(rect.Left, screenBounds.X), Math.Min(rect.Right, screenBounds.Right))
        };

        foreach (var occluder in occluders)
        {
            if (occluder.Top > rect.Top || occluder.Bottom <= rect.Top)
            {
                continue;
            }

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var segment = segments[i];
                var coverLeft = Math.Max(segment.Left, occluder.Left);
                var coverRight = Math.Min(segment.Right, occluder.Right);
                if (coverLeft >= coverRight)
                {
                    continue;
                }

                segments.RemoveAt(i);
                if (segment.Left < coverLeft)
                {
                    segments.Add((segment.Left, coverLeft));
                }

                if (coverRight < segment.Right)
                {
                    segments.Add((coverRight, segment.Right));
                }
            }
        }

        var y = rect.Top - screenBounds.Y;
        var bottom = Math.Min(rect.Bottom, screenBounds.Bottom) - screenBounds.Y;
        foreach (var segment in segments.Where(s => s.Right - s.Left >= 32))
        {
            surfaces.Add(new Surface(
                segment.Left - screenBounds.X,
                segment.Right - screenBounds.X,
                y,
                bottom,
                zOrder));
        }
    }

    public static IntPtr GetHandle(Window window)
    {
        return OperatingSystem.IsWindows()
            ? window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero
            : IntPtr.Zero;
    }

    public static void MakeClickThrough(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var handle = GetHandle(window);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        // Remove native chrome and make the overlay transparent to mouse input.
        var normalStyle = GetWindowLong(handle, GWL_STYLE);
        normalStyle &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER | WS_DLGFRAME);
        normalStyle |= WS_POPUP;
        _ = SetWindowLong(handle, GWL_STYLE, normalStyle);

        var style = GetWindowLong(handle, GWL_EXSTYLE);
        _ = SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
        DisableShadow(handle);
        _ = SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    private static void DisableShadow(IntPtr handle)
    {
        // Suppress the DWM non-client shadow around the transparent overlay.
        var policy = DwmNcrpDisabled;
        _ = DwmSetWindowAttribute(handle, DwmwaNcrenderingPolicy, ref policy, sizeof(int));

        var margins = new Margins();
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);
    }

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_FRAMECHANGED = 0x0020;
    private const int DwmwaNcrenderingPolicy = 2;
    private const int DwmwaCloaked = 14;
    private const int DwmNcrpDisabled = 1;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        int uFlags);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins pMarInset);

    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }
}
