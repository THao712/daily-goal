using System.Drawing.Drawing2D;

namespace DailyGoal;

internal enum AppThemeMode
{
    Dark,
    Light,
    Xuanling
}

internal static class UiTheme
{
    /// <summary>三套主题共用的色板槽位定义；预览色卡与 Apply 都从这里取数，避免字面量分散。</summary>
    internal sealed record Palette(
        Color WindowTop,
        Color WindowBottom,
        Color Canvas,
        Color Surface,
        Color SurfaceHover,
        Color SurfaceMuted,
        Color Border,
        Color BorderStrong,
        Color Text,
        Color TextMuted,
        Color TextFaint,
        Color Coral,
        Color Mint,
        Color Sky,
        Color Sun,
        Color Violet,
        Color Danger,
        Color CardShadow,
        Color CardShadowHover,
        Color AccentSoft,
        Color AccentSoftLine,
        Color AccentText,
        Color AccentHover,
        Color DangerSoft,
        Color SliderTrack,
        Color SliderThumbShadow,
        Color ToggleOffTrack,
        Color SelectionFill);

    private static readonly Palette DarkPalette = new(
        WindowTop: Color.FromArgb(24, 27, 34),
        WindowBottom: Color.FromArgb(20, 23, 29),
        Canvas: Color.FromArgb(28, 32, 40),
        Surface: Color.FromArgb(38, 43, 54),
        SurfaceHover: Color.FromArgb(46, 52, 64),
        SurfaceMuted: Color.FromArgb(32, 36, 45),
        Border: Color.FromArgb(56, 63, 76),
        BorderStrong: Color.FromArgb(76, 85, 100),
        Text: Color.FromArgb(233, 237, 243),
        TextMuted: Color.FromArgb(158, 167, 180),
        TextFaint: Color.FromArgb(110, 119, 133),
        Coral: Color.FromArgb(255, 122, 141),
        Mint: Color.FromArgb(66, 211, 166),
        Sky: Color.FromArgb(108, 167, 255),
        Sun: Color.FromArgb(255, 197, 92),
        Violet: Color.FromArgb(172, 143, 255),
        Danger: Color.FromArgb(240, 98, 120),
        CardShadow: Color.FromArgb(50, 0, 0, 0),
        CardShadowHover: Color.FromArgb(80, 0, 0, 0),
        AccentSoft: Color.FromArgb(36, 54, 49),
        AccentSoftLine: Color.FromArgb(86, 160, 134),
        AccentText: Color.FromArgb(126, 226, 191),
        AccentHover: Color.FromArgb(88, 224, 182),
        DangerSoft: Color.FromArgb(56, 35, 42),
        SliderTrack: Color.FromArgb(110, 96, 106, 122),
        SliderThumbShadow: Color.FromArgb(70, 0, 0, 0),
        ToggleOffTrack: Color.FromArgb(74, 82, 96),
        SelectionFill: Color.FromArgb(40, 51, 68));

    private static readonly Palette LightPalette = new(
        WindowTop: Color.FromArgb(249, 250, 252),
        WindowBottom: Color.FromArgb(246, 248, 250),
        Canvas: Color.FromArgb(242, 245, 248),
        Surface: Color.FromArgb(255, 255, 255),
        SurfaceHover: Color.FromArgb(252, 253, 255),
        SurfaceMuted: Color.FromArgb(243, 245, 247),
        Border: Color.FromArgb(213, 220, 228),
        BorderStrong: Color.FromArgb(185, 195, 206),
        Text: Color.FromArgb(34, 39, 48),
        TextMuted: Color.FromArgb(99, 108, 121),
        TextFaint: Color.FromArgb(145, 153, 164),
        Coral: Color.FromArgb(245, 96, 116),
        Mint: Color.FromArgb(45, 187, 145),
        Sky: Color.FromArgb(71, 139, 236),
        Sun: Color.FromArgb(242, 176, 54),
        Violet: Color.FromArgb(139, 103, 231),
        Danger: Color.FromArgb(211, 72, 94),
        CardShadow: Color.FromArgb(16, 45, 70, 98),
        CardShadowHover: Color.FromArgb(28, 45, 70, 98),
        AccentSoft: Color.FromArgb(235, 248, 244),
        AccentSoftLine: Color.FromArgb(116, 203, 175),
        AccentText: Color.FromArgb(28, 132, 102),
        AccentHover: Color.FromArgb(34, 174, 133),
        DangerSoft: Color.FromArgb(255, 238, 241),
        SliderTrack: Color.FromArgb(98, 182, 194, 207),
        SliderThumbShadow: Color.FromArgb(26, 36, 62, 83),
        ToggleOffTrack: Color.FromArgb(203, 211, 221),
        SelectionFill: Color.FromArgb(232, 242, 252));

    private static readonly Palette XuanlingPalette = new(
        WindowTop: Color.FromArgb(238, 246, 249),
        WindowBottom: Color.FromArgb(222, 237, 244),
        Canvas: Color.FromArgb(215, 232, 241),
        Surface: Color.FromArgb(248, 252, 253),
        SurfaceHover: Color.FromArgb(232, 244, 249),
        SurfaceMuted: Color.FromArgb(205, 225, 235),
        Border: Color.FromArgb(145, 182, 201),
        BorderStrong: Color.FromArgb(105, 151, 177),
        Text: Color.FromArgb(25, 43, 59),
        TextMuted: Color.FromArgb(72, 104, 127),
        TextFaint: Color.FromArgb(111, 139, 158),
        Coral: Color.FromArgb(220, 70, 83),
        Mint: Color.FromArgb(43, 173, 166),
        Sky: Color.FromArgb(46, 139, 203),
        Sun: Color.FromArgb(218, 164, 59),
        Violet: Color.FromArgb(104, 99, 178),
        Danger: Color.FromArgb(205, 59, 73),
        CardShadow: Color.FromArgb(24, 25, 58, 79),
        CardShadowHover: Color.FromArgb(38, 25, 58, 79),
        AccentSoft: Color.FromArgb(215, 239, 247),
        AccentSoftLine: Color.FromArgb(93, 177, 211),
        AccentText: Color.FromArgb(29, 104, 151),
        AccentHover: Color.FromArgb(40, 150, 207),
        DangerSoft: Color.FromArgb(251, 226, 230),
        SliderTrack: Color.FromArgb(112, 126, 163, 183),
        SliderThumbShadow: Color.FromArgb(34, 34, 74, 96),
        ToggleOffTrack: Color.FromArgb(154, 181, 197),
        SelectionFill: Color.FromArgb(198, 226, 240));

    public static AppThemeMode Mode { get; private set; }

    public static Color WindowTop { get; private set; }
    public static Color WindowBottom { get; private set; }
    public static Color Canvas { get; private set; }
    public static Color Surface { get; private set; }
    public static Color SurfaceHover { get; private set; }
    public static Color SurfaceMuted { get; private set; }
    public static Color Border { get; private set; }
    public static Color BorderStrong { get; private set; }
    public static Color Text { get; private set; }
    public static Color TextMuted { get; private set; }
    public static Color TextFaint { get; private set; }
    public static Color Coral { get; private set; }
    public static Color Mint { get; private set; }
    public static Color Sky { get; private set; }
    public static Color Sun { get; private set; }
    public static Color Violet { get; private set; }
    public static Color Danger { get; private set; }

    public static Color CardShadow { get; private set; }
    public static Color CardShadowHover { get; private set; }
    public static Color AccentSoft { get; private set; }
    public static Color AccentSoftLine { get; private set; }
    public static Color AccentText { get; private set; }
    public static Color AccentHover { get; private set; }
    public static Color DangerSoft { get; private set; }
    public static Color SliderTrack { get; private set; }
    public static Color SliderThumbShadow { get; private set; }
    public static Color ToggleOffTrack { get; private set; }
    public static Color SelectionFill { get; private set; }

    public static Color[] Accents { get; private set; } = [];

    /// <summary>任务强调色的中文名，与 Accents 顺序一一对应（颜色菜单使用）。</summary>
    public static readonly string[] AccentNames = ["珊瑚", "薄荷", "晴空", "暖阳", "紫罗兰"];

    public static float RadiusSmall => 6f * Scale;
    public static float RadiusMedium => 8f * Scale;
    public static float RadiusLarge => 10f * Scale;

    public static bool ReduceMotion;

    /// <summary>全局 UI 缩放系数：布局常量与字体统一乘它；启动时确定，运行期不变。</summary>
    public static float Scale { get; private set; } = 1f;

    /// <summary>布局常量按缩放取整。</summary>
    public static int S(float value)
    {
        return (int)MathF.Round(value * Scale);
    }

    /// <summary>浮点度量按缩放。</summary>
    public static float Sf(float value)
    {
        return value * Scale;
    }

    static UiTheme()
    {
        RebuildFonts();
        Apply(AppThemeMode.Dark);
    }

    /// <summary>必须在创建任何控件前调用（字体与布局常量在控件构造时就被读取）。</summary>
    public static void ApplyScale(float scale)
    {
        var clamped = Math.Clamp(scale, UiScaleResolver.MinimumScale, 2f);
        if (Math.Abs(clamped - Scale) < 0.01f)
        {
            return;
        }

        Scale = clamped;
        RebuildFonts();
    }

    public static Palette PaletteFor(AppThemeMode mode)
    {
        return mode switch
        {
            AppThemeMode.Light => LightPalette,
            AppThemeMode.Xuanling => XuanlingPalette,
            _ => DarkPalette
        };
    }

    public static void Apply(AppThemeMode mode)
    {
        Mode = mode;
        var palette = PaletteFor(mode);
        WindowTop = palette.WindowTop;
        WindowBottom = palette.WindowBottom;
        Canvas = palette.Canvas;
        Surface = palette.Surface;
        SurfaceHover = palette.SurfaceHover;
        SurfaceMuted = palette.SurfaceMuted;
        Border = palette.Border;
        BorderStrong = palette.BorderStrong;
        Text = palette.Text;
        TextMuted = palette.TextMuted;
        TextFaint = palette.TextFaint;
        Coral = palette.Coral;
        Mint = palette.Mint;
        Sky = palette.Sky;
        Sun = palette.Sun;
        Violet = palette.Violet;
        Danger = palette.Danger;
        CardShadow = palette.CardShadow;
        CardShadowHover = palette.CardShadowHover;
        AccentSoft = palette.AccentSoft;
        AccentSoftLine = palette.AccentSoftLine;
        AccentText = palette.AccentText;
        AccentHover = palette.AccentHover;
        DangerSoft = palette.DangerSoft;
        SliderTrack = palette.SliderTrack;
        SliderThumbShadow = palette.SliderThumbShadow;
        ToggleOffTrack = palette.ToggleOffTrack;
        SelectionFill = palette.SelectionFill;
        Accents = [Coral, Mint, Sky, Sun, Violet];
    }

    /// <summary>主题切换时用于旧色→新色映射；两套色板按同一顺序返回。</summary>
    public static Color[] PaletteSnapshot()
    {
        return
        [
            WindowTop, WindowBottom, Canvas, Surface, SurfaceHover, SurfaceMuted,
            Border, BorderStrong, Text, TextMuted, TextFaint,
            Coral, Mint, Sky, Sun, Violet, Danger
        ];
    }

    public static Font TitleFont { get; private set; } = null!;
    public static Font HeadingFont { get; private set; } = null!;
    public static Font BodyFont { get; private set; } = null!;
    public static Font BodyBoldFont { get; private set; } = null!;
    public static Font TaskFont { get; private set; } = null!;
    public static Font SmallFont { get; private set; } = null!;
    public static Font SmallBoldFont { get; private set; } = null!;

    private static void RebuildFonts()
    {
        TitleFont = MakeFont(31f, FontStyle.Bold);
        HeadingFont = MakeFont(19f, FontStyle.Bold);
        BodyFont = MakeFont(18f, FontStyle.Regular);
        BodyBoldFont = MakeFont(18f, FontStyle.Bold);
        TaskFont = MakeFont(22f, FontStyle.Regular);
        SmallFont = MakeFont(15f, FontStyle.Regular);
        SmallBoldFont = MakeFont(15f, FontStyle.Bold);
    }

    private static Font MakeFont(float basePixelSize, FontStyle style)
    {
        return new Font("Microsoft YaHei UI", basePixelSize * Scale, style, GraphicsUnit.Pixel);
    }

    public static Color Accent(int index)
    {
        return Accents[Math.Abs(index) % Accents.Length];
    }

    public static Color WithAlpha(Color color, int alpha)
    {
        return Color.FromArgb(Math.Clamp(alpha, 0, 255), color);
    }

    /// <summary>亮强调色（暖阳、薄荷）上用深色文字，其余用白色，保证对比度。</summary>
    public static Color OnAccent(Color accent)
    {
        var luminance = 0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B;
        return luminance > 160 ? Color.FromArgb(24, 27, 34) : Color.White;
    }

    public static Color Blend(Color from, Color to, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(from.R + (to.R - from.R) * t),
            (int)(from.G + (to.G - from.G) * t),
            (int)(from.B + (to.B - from.B) * t));
    }

    public static void DrawFocusRing(Graphics graphics, GraphicsPath path)
    {
        using var pen = new Pen(WithAlpha(Sky, 210), 1.6f);
        graphics.DrawPath(pen, path);
    }

    public static void StyleMenu(ContextMenuStrip menu)
    {
        menu.Renderer = new DarkMenuRenderer();
        menu.BackColor = Surface;
        menu.ForeColor = Text;
        menu.HandleCreated += (_, _) => NativeMethods.ApplyRoundedCorners(menu.Handle, small: true);

        var fadeTimer = new System.Windows.Forms.Timer { Interval = 15 };
        fadeTimer.Tick += (_, _) =>
        {
            if (!menu.Visible || menu.Opacity >= 1)
            {
                fadeTimer.Stop();
                menu.Opacity = 1;
                return;
            }

            menu.Opacity = Math.Min(1, menu.Opacity + 0.22);
        };
        menu.Opening += (_, _) =>
        {
            if (!ReduceMotion)
            {
                menu.Opacity = 0.15;
            }
        };
        menu.Opened += (_, _) =>
        {
            if (ReduceMotion)
            {
                menu.Opacity = 1;
            }
            else
            {
                fadeTimer.Start();
            }
        };
        menu.Disposed += (_, _) => fadeTimer.Dispose();
    }
}

internal static class UiScaleResolver
{
    internal const float MinimumScale = 0.68f;
    private const float DesignWidth = 600f;
    private const float DesignHeight = 840f;
    private const float MaximumWorkAreaFraction = 0.52f;
    private const float VerticalMargin = 28f;

    public static float Resolve(int preferredPercent, Rectangle workArea)
    {
        var widthLimit = workArea.Width * MaximumWorkAreaFraction / DesignWidth;
        var heightLimit = (workArea.Height - VerticalMargin) / DesignHeight;
        var safeLimit = Math.Clamp(Math.Min(widthLimit, heightLimit), MinimumScale, 2f);
        var preferredScale = preferredPercent <= 0 ? 1f : preferredPercent / 100f;
        return Math.Clamp(Math.Min(preferredScale, safeLimit), MinimumScale, 2f);
    }
}

internal sealed class DarkMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => UiTheme.Surface;
    public override Color ImageMarginGradientBegin => UiTheme.Surface;
    public override Color ImageMarginGradientMiddle => UiTheme.Surface;
    public override Color ImageMarginGradientEnd => UiTheme.Surface;
    public override Color MenuBorder => UiTheme.Border;
    public override Color MenuItemBorder => UiTheme.SurfaceHover;
    public override Color MenuItemSelected => UiTheme.SurfaceHover;
    public override Color MenuItemSelectedGradientBegin => UiTheme.SurfaceHover;
    public override Color MenuItemSelectedGradientEnd => UiTheme.SurfaceHover;
    public override Color MenuItemPressedGradientBegin => UiTheme.SurfaceMuted;
    public override Color MenuItemPressedGradientMiddle => UiTheme.SurfaceMuted;
    public override Color MenuItemPressedGradientEnd => UiTheme.SurfaceMuted;
    public override Color SeparatorDark => UiTheme.Border;
    public override Color SeparatorLight => UiTheme.Surface;
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer()
        : base(new DarkMenuColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? UiTheme.Text : UiTheme.TextFaint;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item is { Enabled: true } ? UiTheme.Text : UiTheme.TextFaint;
        base.OnRenderArrow(e);
    }
}

internal enum IconKind
{
    Add,
    Settings,
    More,
    Close,
    Back,
    Edit,
    Delete,
    Restore,
    Expand,
    Template,
    Repeat
}

internal static class IconPainter
{
    public static void Draw(Graphics graphics, IconKind icon, RectangleF bounds, Color color, float strokeWidth = 1.8f)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(bounds.Width, bounds.Height);
        var scale = size / 20f;
        var centerX = bounds.Left + bounds.Width / 2f;
        var centerY = bounds.Top + bounds.Height / 2f;
        using var pen = new Pen(color, Math.Max(1.4f, strokeWidth * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        PointF P(float x, float y) => new(centerX + x * scale, centerY + y * scale);

        switch (icon)
        {
            case IconKind.Add:
                graphics.DrawLine(pen, P(-5.5f, 0), P(5.5f, 0));
                graphics.DrawLine(pen, P(0, -5.5f), P(0, 5.5f));
                break;
            case IconKind.Settings:
                graphics.DrawEllipse(pen, centerX - 3.2f * scale, centerY - 3.2f * scale, 6.4f * scale, 6.4f * scale);
                graphics.DrawEllipse(pen, centerX - 6.2f * scale, centerY - 6.2f * scale, 12.4f * scale, 12.4f * scale);
                for (var index = 0; index < 8; index += 1)
                {
                    var angle = index * Math.PI / 4;
                    var inner = 6.3f;
                    var outer = 8.1f;
                    graphics.DrawLine(
                        pen,
                        P((float)Math.Cos(angle) * inner, (float)Math.Sin(angle) * inner),
                        P((float)Math.Cos(angle) * outer, (float)Math.Sin(angle) * outer));
                }
                break;
            case IconKind.More:
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, centerX - 6.4f * scale, centerY - 1.35f * scale, 2.7f * scale, 2.7f * scale);
                    graphics.FillEllipse(brush, centerX - 1.35f * scale, centerY - 1.35f * scale, 2.7f * scale, 2.7f * scale);
                    graphics.FillEllipse(brush, centerX + 3.7f * scale, centerY - 1.35f * scale, 2.7f * scale, 2.7f * scale);
                }
                break;
            case IconKind.Close:
                graphics.DrawLine(pen, P(-5, -5), P(5, 5));
                graphics.DrawLine(pen, P(5, -5), P(-5, 5));
                break;
            case IconKind.Back:
                graphics.DrawLines(pen, [P(2.5f, -6), P(-3.5f, 0), P(2.5f, 6)]);
                graphics.DrawLine(pen, P(-3, 0), P(6, 0));
                break;
            case IconKind.Edit:
                graphics.DrawLines(pen, [P(-5.5f, 4.5f), P(-4.3f, 0.5f), P(3.8f, -7.6f), P(7.2f, -4.2f), P(-0.9f, 3.9f), P(-5.5f, 4.5f)]);
                graphics.DrawLine(pen, P(2.5f, -6.3f), P(5.9f, -2.9f));
                break;
            case IconKind.Delete:
                graphics.DrawLine(pen, P(-5.8f, -4.5f), P(5.8f, -4.5f));
                graphics.DrawLine(pen, P(-2.5f, -7f), P(2.5f, -7f));
                graphics.DrawLines(pen, [P(-4.7f, -2.5f), P(-3.7f, 6.5f), P(3.7f, 6.5f), P(4.7f, -2.5f)]);
                graphics.DrawLine(pen, P(-1.5f, -1.5f), P(-1.1f, 4.3f));
                graphics.DrawLine(pen, P(1.5f, -1.5f), P(1.1f, 4.3f));
                break;
            case IconKind.Restore:
                graphics.DrawArc(pen, centerX - 6.2f * scale, centerY - 6.2f * scale, 12.4f * scale, 12.4f * scale, -55, 285);
                graphics.DrawLines(pen, [P(-7.2f, -3.8f), P(-7.4f, 1.4f), P(-2.3f, 0.2f)]);
                break;
            case IconKind.Expand:
                graphics.DrawLines(pen, [P(3, -6), P(-3, 0), P(3, 6)]);
                break;
            case IconKind.Template:
                graphics.DrawLines(pen, [P(-5, -7), P(5, -7), P(5, 7), P(0, 3.5f), P(-5, 7), P(-5, -7)]);
                break;
            case IconKind.Repeat:
                graphics.DrawArc(pen, centerX - 6 * scale, centerY - 5 * scale, 12 * scale, 10 * scale, 205, 235);
                graphics.DrawLines(pen, [P(4, -5), P(7, -4), P(5.5f, -1)]);
                break;
        }
    }
}

internal static class PaintHelpers
{
    public static GraphicsPath RoundRect(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(rectangle);
            return path;
        }

        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>沿卡片上缘画一条细微的内高光，模拟顶光照射的立体感（浅色主题下无需高光）。</summary>
    public static void DrawTopHighlight(Graphics graphics, RectangleF rectangle, float radius, int alpha = 16)
    {
        if (UiTheme.Mode == AppThemeMode.Light || rectangle.Width <= radius * 2)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(alpha, 255, 255, 255), 1f);
        graphics.DrawLine(
            pen,
            rectangle.Left + radius,
            rectangle.Top + 1f,
            rectangle.Right - radius,
            rectangle.Top + 1f);
    }

    public static GraphicsPath StarPath(float centerX, float centerY, float outerRadius, float innerRadius, int points)
    {
        var path = new GraphicsPath();
        var vertices = new PointF[points * 2];
        for (var index = 0; index < vertices.Length; index += 1)
        {
            var radius = index % 2 == 0 ? outerRadius : innerRadius;
            var angle = -Math.PI / 2 + index * Math.PI / points;
            vertices[index] = new PointF(
                centerX + (float)Math.Cos(angle) * radius,
                centerY + (float)Math.Sin(angle) * radius);
        }

        path.AddPolygon(vertices);
        return path;
    }
}
