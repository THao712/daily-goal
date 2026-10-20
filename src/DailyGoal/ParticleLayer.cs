using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DailyGoal;

internal enum ParticleShape
{
    Dot,
    Spark,
    Star,
    Ring,
    Diamond
}

internal sealed class Particle
{
    public Color Color { get; init; }
    public ParticleShape Shape { get; init; }
    public float Size { get; init; } = 1f;
    public double X { get; set; }
    public double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Age { get; set; }
    public int Life { get; init; }
    public double Rotation { get; set; }
    public double Spin { get; init; }
    public float Opacity { get; set; } = 1f;
    public float VisualScale { get; set; } = 1f;
    public bool Accent { get; init; }
    public bool HasTrail { get; init; }
}

internal sealed class ParticleLayer : Form
{
    /// <summary>窗口四周的飞行余量：按粒子物理参数（初速、衰减、重力、寿命）估算的最大飞行距离。</summary>
    private const int FlightPadding = 480;

    private readonly List<Particle> particles;
    private readonly GraphicsPath starPath = PaintHelpers.StarPath(0, 0, 8f, 3.6f, 5);
    private readonly GraphicsPath diamondPath = CreateDiamondPath();
    private Bitmap? frameBitmap;
    private Graphics? frameGraphics;
    private PointF renderOffset;
    private IntPtr surfaceDc;
    private IntPtr dibSection;
    private IntPtr previousSurfaceBitmap;
    private Size surfaceSize;
    private bool resourcesDisposed;

    public int RenderCount { get; private set; }
    public bool LastRenderSucceeded { get; private set; }

    public ParticleLayer(List<Particle> particles)
    {
        this.particles = particles;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
        TabStop = false;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExLayered = 0x00080000;
            const int wsExTransparent = 0x20;
            const int wsExToolWindow = 0x80;
            const int wsExNoActivate = 0x08000000;
            var createParams = base.CreateParams;
            createParams.ExStyle |= wsExLayered | wsExTransparent | wsExToolWindow | wsExNoActivate;
            return createParams;
        }
    }

    public void ShowFor(Form owner)
    {
        if (owner.IsDisposed || !owner.Visible || particles.Count == 0)
        {
            return;
        }

        UpdateBounds(owner);
        if (!IsHandleCreated)
        {
            _ = Handle;
        }

        if (RenderLayeredBitmap() && !Visible)
        {
            Show(owner);
        }
    }

    public void UpdateFor(Form owner)
    {
        if (owner.IsDisposed || !owner.Visible || particles.Count == 0)
        {
            Hide();
            return;
        }

        UpdateBounds(owner);
        if (RenderLayeredBitmap() && !Visible)
        {
            Show(owner);
        }
    }

    public void RepositionFor(Form owner)
    {
        if (owner.IsDisposed || !owner.Visible || particles.Count == 0)
        {
            Hide();
            return;
        }

        UpdateBounds(owner);
    }

    private void UpdateBounds(Form owner)
    {
        // 覆盖宿主客户区并向四周扩出飞行余量（裁剪到所在屏幕），让粒子能自然飞出窗口；
        // 边界在整场爆发期间保持稳定，绘制表面无需反复重建。
        var ownerBounds = new Rectangle(
            owner.Left,
            owner.Top,
            Math.Max(1, owner.ClientSize.Width),
            Math.Max(1, owner.ClientSize.Height));
        var bounds = Rectangle.Inflate(ownerBounds, FlightPadding, FlightPadding);
        bounds.Intersect(Screen.FromRectangle(ownerBounds).Bounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = ownerBounds;
        }

        renderOffset = new PointF(ownerBounds.Left - bounds.Left, ownerBounds.Top - bounds.Top);
        if (Bounds != bounds)
        {
            Bounds = bounds;
        }
    }

    private bool RenderLayeredBitmap()
    {
        if (Width <= 0 || Height <= 0 || !IsHandleCreated)
        {
            LastRenderSucceeded = false;
            return false;
        }

        try
        {
            if (!EnsureFrameSurface())
            {
                LastRenderSucceeded = false;
                return false;
            }

            var graphics = frameGraphics!;
            graphics.ResetTransform();
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;

            foreach (var particle in particles)
            {
                graphics.ResetTransform();
                graphics.TranslateTransform((float)particle.X + renderOffset.X, (float)particle.Y + renderOffset.Y);
                graphics.RotateTransform((float)particle.Rotation);
                graphics.ScaleTransform(particle.VisualScale, particle.VisualScale);
                var alpha = (int)(Math.Clamp(particle.Opacity, 0f, 1f) * 255);
                DrawTrail(graphics, particle, alpha);
                DrawGlow(graphics, particle, alpha);
                DrawShape(graphics, particle, alpha);
            }

            graphics.ResetTransform();
            graphics.Flush(FlushIntention.Sync);
            LastRenderSucceeded = ApplyLayeredSurface();
            if (LastRenderSucceeded)
            {
                RenderCount += 1;
            }

            return LastRenderSucceeded;
        }
        catch (Exception ex) when (ex is ExternalException or OutOfMemoryException or ArgumentException)
        {
            LastRenderSucceeded = false;
            return false;
        }
    }

    /// <summary>
    /// 绘制表面是一块 DIB section：GDI+ 直接画进它的内存，UpdateLayeredWindow 直接从
    /// 同一块内存合成，整个渲染路径零位图拷贝、零逐帧 GDI 对象分配。
    /// </summary>
    private bool EnsureFrameSurface()
    {
        if (surfaceDc != IntPtr.Zero && surfaceSize.Width == Width && surfaceSize.Height == Height)
        {
            return true;
        }

        ReleaseFrameSurface();
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            surfaceDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (surfaceDc == IntPtr.Zero)
            {
                return false;
            }

            var info = new NativeMethods.BitmapInfoHeader
            {
                Size = Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                Width = Width,
                Height = -Height,
                Planes = 1,
                BitCount = 32
            };
            dibSection = NativeMethods.CreateDIBSection(surfaceDc, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (dibSection == IntPtr.Zero || bits == IntPtr.Zero)
            {
                ReleaseFrameSurface();
                return false;
            }

            previousSurfaceBitmap = NativeMethods.SelectObject(surfaceDc, dibSection);
            frameBitmap = new Bitmap(Width, Height, Width * 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, bits);
            frameGraphics = Graphics.FromImage(frameBitmap);
            surfaceSize = new Size(Width, Height);
            return true;
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void ReleaseFrameSurface()
    {
        frameGraphics?.Dispose();
        frameGraphics = null;
        frameBitmap?.Dispose();
        frameBitmap = null;
        if (surfaceDc != IntPtr.Zero && previousSurfaceBitmap != IntPtr.Zero)
        {
            _ = NativeMethods.SelectObject(surfaceDc, previousSurfaceBitmap);
            previousSurfaceBitmap = IntPtr.Zero;
        }

        if (dibSection != IntPtr.Zero)
        {
            _ = NativeMethods.DeleteObject(dibSection);
            dibSection = IntPtr.Zero;
        }

        if (surfaceDc != IntPtr.Zero)
        {
            _ = NativeMethods.DeleteDC(surfaceDc);
            surfaceDc = IntPtr.Zero;
        }

        surfaceSize = Size.Empty;
    }

    private bool ApplyLayeredSurface()
    {
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var size = new NativeMethods.Size(surfaceSize.Width, surfaceSize.Height);
            var source = new NativeMethods.Point(0, 0);
            var destination = new NativeMethods.Point(Left, Top);
            var blend = new NativeMethods.BlendFunction
            {
                BlendOp = NativeMethods.AcSrcOver,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AcSrcAlpha
            };

            return NativeMethods.UpdateLayeredWindow(
                Handle,
                screenDc,
                ref destination,
                ref size,
                surfaceDc,
                ref source,
                0,
                ref blend,
                NativeMethods.UlwAlpha);
        }
        catch (Exception ex) when (ex is ExternalException or OutOfMemoryException)
        {
            return false;
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static void DrawTrail(Graphics graphics, Particle particle, int alpha)
    {
        if (!particle.HasTrail || particle.Shape == ParticleShape.Ring || alpha < 24)
        {
            return;
        }

        var speed = Math.Sqrt(particle.Vx * particle.Vx + particle.Vy * particle.Vy);
        if (speed < 0.1)
        {
            return;
        }

        var tail = Math.Clamp(speed * 1.15, 5.8, particle.Accent ? 16.5 : 11.5);
        using var trailPen = new Pen(
            Color.FromArgb(Math.Max(0, particle.Accent ? alpha / 3 : alpha / 5), particle.Color),
            particle.Accent ? 1.75f : 1.15f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(
            trailPen,
            0,
            0,
            (float)(-particle.Vx / speed * tail),
            (float)(-particle.Vy / speed * tail));
    }

    private static void DrawGlow(Graphics graphics, Particle particle, int alpha)
    {
        if (!particle.Accent || particle.Shape == ParticleShape.Ring || alpha < 36)
        {
            return;
        }

        using var glow = new SolidBrush(Color.FromArgb(Math.Max(0, alpha / 7), particle.Color));
        graphics.FillEllipse(glow, -8.5f, -8.5f, 17f, 17f);
        using var core = new SolidBrush(Color.FromArgb(Math.Min(210, alpha), Color.White));
        graphics.FillEllipse(core, -1.25f, -1.25f, 2.5f, 2.5f);
    }

    private void DrawShape(Graphics graphics, Particle particle, int alpha)
    {
        var color = Color.FromArgb(alpha, particle.Color);
        switch (particle.Shape)
        {
            case ParticleShape.Spark:
                using (var pen = new Pen(color, particle.Accent ? 2.6f : 1.8f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                {
                    var length = particle.Accent ? 8.5f : 6.2f;
                    graphics.DrawLine(pen, 0, -length, 0, length);
                }
                break;
            case ParticleShape.Star:
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillPath(brush, starPath);
                }
                break;
            case ParticleShape.Ring:
                using (var pen = new Pen(color, 1.8f))
                {
                    graphics.DrawEllipse(pen, -6.5f, -6.5f, 13f, 13f);
                }
                break;
            case ParticleShape.Diamond:
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillPath(brush, diamondPath);
                }
                break;
            default:
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, -4.4f, -4.4f, 8.8f, 8.8f);
                }
                break;
        }
    }

    private static GraphicsPath CreateDiamondPath()
    {
        var path = new GraphicsPath();
        path.AddPolygon([
            new PointF(0, -7.5f),
            new PointF(7.5f, 0),
            new PointF(0, 7.5f),
            new PointF(-7.5f, 0)]);
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            resourcesDisposed = true;
            ReleaseFrameSurface();
            starPath.Dispose();
            diamondPath.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal static class NativeMethods
{
    internal const byte AcSrcOver = 0x00;
    internal const byte AcSrcAlpha = 0x01;
    internal const int UlwAlpha = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Size
    {
        public int Cx;
        public int Cy;

        public Size(int width, int height)
        {
            Cx = width;
            Cy = height;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfoHeader info,
        uint usage,
        out IntPtr bits,
        IntPtr fileMapping,
        uint offset);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    internal static extern uint TimeBeginPeriod(uint period);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    internal static extern uint TimeEndPeriod(uint period);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int RegisterWindowMessage(string messageName);

    internal const int WmHotkey = 0x0312;
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModNoRepeat = 0x4000;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr windowHandle, string? subAppName, string? subIdList);

    /// <summary>给带原生滚动条的控件（ListBox 等）套系统暗色/亮色滚动条主题；旧系统静默跳过。</summary>
    internal static void ApplyScrollBarTheme(IntPtr windowHandle, bool dark)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _ = SetWindowTheme(windowHandle, dark ? "DarkMode_Explorer" : "Explorer", null);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("dwmapi.dll")]
    internal static extern int DwmFlush();

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int DwmwcpRoundSmall = 3;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int size);

    /// <summary>Windows 11 的抗锯齿圆角 + 系统投影；旧系统返回 false，调用方回退到 Region 裁剪。</summary>
    internal static bool ApplyRoundedCorners(IntPtr windowHandle, bool small = false)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var preference = small ? DwmwcpRoundSmall : DwmwcpRound;
        try
        {
            return DwmSetWindowAttribute(windowHandle, DwmwaWindowCornerPreference, ref preference, sizeof(int)) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetDC(IntPtr windowHandle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool DeleteDC(IntPtr deviceContext);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool DeleteObject(IntPtr graphicsObject);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UpdateLayeredWindow(
        IntPtr windowHandle,
        IntPtr destinationDeviceContext,
        ref Point destinationPoint,
        ref Size size,
        IntPtr sourceDeviceContext,
        ref Point sourcePoint,
        int colorKey,
        ref BlendFunction blend,
        int flags);
}
