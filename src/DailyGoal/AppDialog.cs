using System.Drawing.Drawing2D;

namespace DailyGoal;

internal enum DialogTone
{
    Neutral,
    Primary,
    Danger
}

/// <summary>
/// 与主题一致的自绘模态对话框，替代系统 MessageBox：
/// 按钮文字直接表达动作（如「仅跳过今天 / 停止重复 / 取消」），返回被点按钮的下标，Esc/关闭返回 -1。
/// </summary>
internal sealed class AppDialog : Form
{
    private readonly Label titleLabel = new();
    private readonly Label messageLabel = new();
    private int result = -1;
    private bool dwmCornersApplied;

    protected override CreateParams CreateParams
    {
        get
        {
            const int csDropShadow = 0x00020000;
            var createParams = base.CreateParams;
            createParams.ClassStyle |= csDropShadow;
            return createParams;
        }
    }

    private AppDialog(string title, string message, IReadOnlyList<(string Text, DialogTone Tone)> buttons)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = UiTheme.WindowTop;
        Font = UiTheme.BodyFont;

        var width = UiTheme.S(432);
        var padding = UiTheme.S(28);
        var contentWidth = width - padding * 2;

        titleLabel.Text = title;
        titleLabel.Font = UiTheme.HeadingFont;
        titleLabel.ForeColor = UiTheme.Text;
        titleLabel.BackColor = UiTheme.WindowTop;
        titleLabel.Location = new Point(padding, UiTheme.S(24));
        titleLabel.Size = new Size(contentWidth, UiTheme.S(30));
        Controls.Add(titleLabel);

        var messageHeight = TextRenderer.MeasureText(
            message,
            UiTheme.BodyFont,
            new Size(contentWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        messageLabel.Text = message;
        messageLabel.Font = UiTheme.BodyFont;
        messageLabel.ForeColor = UiTheme.TextMuted;
        messageLabel.BackColor = UiTheme.WindowTop;
        messageLabel.Location = new Point(padding, UiTheme.S(62));
        messageLabel.Size = new Size(contentWidth, messageHeight + UiTheme.S(6));
        Controls.Add(messageLabel);

        var buttonTop = messageLabel.Bottom + UiTheme.S(22);
        var buttonHeight = UiTheme.S(44);
        var gap = UiTheme.S(10);
        var x = width - padding;
        for (var index = buttons.Count - 1; index >= 0; index -= 1)
        {
            var (text, tone) = buttons[index];
            var buttonWidth = Math.Max(
                UiTheme.S(92),
                TextRenderer.MeasureText(text, UiTheme.BodyBoldFont).Width + UiTheme.S(36));
            var button = new DialogButton(text, tone, index);
            button.Size = new Size(buttonWidth, buttonHeight);
            x -= buttonWidth;
            button.Location = new Point(x, buttonTop);
            x -= gap;
            button.Activated += (_, _) =>
            {
                result = button.Index;
                Close();
            };
            Controls.Add(button);
        }

        ClientSize = new Size(width, buttonTop + buttonHeight + UiTheme.S(24));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        dwmCornersApplied = NativeMethods.ApplyRoundedCorners(Handle);
        if (dwmCornersApplied)
        {
            Region?.Dispose();
            Region = null;
        }
        else
        {
            using var path = PaintHelpers.RoundRect(new RectangleF(0, 0, Width, Height), UiTheme.RadiusLarge);
            Region?.Dispose();
            Region = new Region(path);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var primary = Controls.OfType<DialogButton>().OrderBy(button => button.Index)
            .FirstOrDefault(button => button.Tone != DialogTone.Neutral)
            ?? Controls.OfType<DialogButton>().OrderBy(button => button.Index).FirstOrDefault();
        primary?.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            result = -1;
            Close();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusLarge);
        using var pen = new Pen(UiTheme.Border, 1f);
        e.Graphics.DrawPath(pen, path);
        PaintHelpers.DrawTopHighlight(e.Graphics, rect, UiTheme.RadiusLarge);
    }

    private void PositionOver(IWin32Window? owner)
    {
        var anchor = owner as Control;
        var screen = anchor is null ? Screen.PrimaryScreen : Screen.FromControl(anchor);
        var area = (screen ?? Screen.PrimaryScreen)?.WorkingArea ?? new Rectangle(0, 0, 1200, 800);
        Point target;
        if (anchor is Form form && form.Visible)
        {
            target = new Point(
                form.Left + (form.Width - Width) / 2,
                form.Top + (form.Height - Height) / 3);
        }
        else
        {
            target = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 3);
        }

        Location = new Point(
            Math.Clamp(target.X, area.Left + 8, Math.Max(area.Left + 8, area.Right - Width - 8)),
            Math.Clamp(target.Y, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - Height - 8)));
    }

    /// <summary>展示多按钮选择，返回按钮下标；Esc/关闭返回 -1。</summary>
    public static int Choose(
        IWin32Window? owner,
        string title,
        string message,
        params (string Text, DialogTone Tone)[] buttons)
    {
        using var dialog = new AppDialog(title, message, buttons);
        dialog.PositionOver(owner);
        if (owner is null)
        {
            dialog.ShowDialog();
        }
        else
        {
            dialog.ShowDialog(owner);
        }

        return dialog.result;
    }

    public static bool Confirm(
        IWin32Window? owner,
        string title,
        string message,
        string confirmText = "确定",
        bool danger = false)
    {
        return Choose(
            owner,
            title,
            message,
            (confirmText, danger ? DialogTone.Danger : DialogTone.Primary),
            ("取消", DialogTone.Neutral)) == 0;
    }

    public static void Info(IWin32Window? owner, string title, string message)
    {
        _ = Choose(owner, title, message, ("好的", DialogTone.Primary));
    }

    private sealed class DialogButton : Control
    {
        private bool hovered;
        private bool pressed;

        public DialogTone Tone { get; }
        public int Index { get; }

        public event EventHandler? Activated;

        public DialogButton(string text, DialogTone tone, int index)
        {
            Text = text;
            Tone = tone;
            Index = index;
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.Selectable
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer,
                true);
            BackColor = UiTheme.WindowTop;
            Cursor = Cursors.Hand;
            TabStop = true;
            TabIndex = index;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            var fire = e.Button == MouseButtons.Left && pressed && ClientRectangle.Contains(e.Location);
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
            if (fire)
            {
                Activated?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Return or Keys.Space)
            {
                Activated?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(BackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var offset = pressed ? 1f : 0f;
            var rect = new RectangleF(1f, 1f + offset, Width - 2f, Height - 2f - offset);
            using var path = PaintHelpers.RoundRect(rect, UiTheme.RadiusMedium);
            var fill = Tone switch
            {
                DialogTone.Primary => hovered ? UiTheme.AccentHover : UiTheme.Mint,
                DialogTone.Danger => hovered ? UiTheme.Danger : UiTheme.WithAlpha(UiTheme.Danger, 230),
                _ => hovered ? UiTheme.SurfaceHover : UiTheme.Surface
            };
            using (var brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
            }

            if (Tone == DialogTone.Neutral)
            {
                using var pen = new Pen(UiTheme.Border, 1f);
                e.Graphics.DrawPath(pen, path);
            }

            var textColor = Tone == DialogTone.Neutral ? UiTheme.Text : UiTheme.OnAccent(fill);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                UiTheme.BodyBoldFont,
                Rectangle.Round(rect),
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues)
            {
                UiTheme.DrawFocusRing(e.Graphics, path);
            }
        }
    }
}
