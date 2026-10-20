namespace DailyGoal;

/// <summary>
/// 垂直堆叠任意子控件并用 SlimScrollBar 滚动的容器（历史列表、设置页内容都用它）。
/// 子控件宽度由调用方设置，容器按 Margin.Top/Bottom 依次排布；添加/删除后调用 Relayout()。
/// </summary>
internal sealed class StackScrollPanel : Panel
{
    private readonly SlimScrollBar scrollBar = new();
    private readonly List<Control> stack = [];
    private bool arranging;

    public StackScrollPanel()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        scrollBar.ValueChanged += (_, _) => Relayout();
        Controls.Add(scrollBar);
        scrollBar.BringToFront();
    }

    public int ScrollValue => scrollBar.Value;

    public void ScrollByWheel(int delta)
    {
        if (scrollBar.Visible && delta != 0)
        {
            scrollBar.Value -= Math.Sign(delta) * UiTheme.S(58);
        }
    }

    public void ClearStack()
    {
        arranging = true;
        try
        {
            foreach (var child in stack.ToArray())
            {
                Controls.Remove(child);
                child.Dispose();
            }
        }
        finally
        {
            arranging = false;
        }

        stack.Clear();
        scrollBar.Value = 0;
        Relayout();
    }

    public void Relayout()
    {
        if (arranging || IsDisposed)
        {
            return;
        }

        arranging = true;
        try
        {
            var contentHeight = Padding.Top + Padding.Bottom;
            foreach (var child in stack)
            {
                contentHeight += child.Margin.Top + child.Height + child.Margin.Bottom;
            }

            var needsScroll = contentHeight > ClientSize.Height;
            scrollBar.Visible = needsScroll;
            scrollBar.Bounds = new Rectangle(Math.Max(0, ClientSize.Width - UiTheme.S(10)), 0, UiTheme.S(10), ClientSize.Height);
            if (needsScroll)
            {
                scrollBar.Minimum = 0;
                scrollBar.LargeChange = Math.Max(1, ClientSize.Height);
                scrollBar.Maximum = Math.Max(scrollBar.LargeChange, contentHeight - 1);
            }
            else
            {
                scrollBar.Value = 0;
            }

            var y = Padding.Top - scrollBar.Value;
            foreach (var child in stack)
            {
                y += child.Margin.Top;
                child.Location = new Point(Padding.Left + child.Margin.Left, y);
                y += child.Height + child.Margin.Bottom;
            }

            scrollBar.BringToFront();
        }
        finally
        {
            arranging = false;
        }
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is null || e.Control == scrollBar)
        {
            return;
        }

        var child = e.Control;
        if (!stack.Contains(child))
        {
            stack.Add(child);
            child.Disposed += (_, _) => stack.Remove(child);
        }
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control is not null && e.Control != scrollBar)
        {
            stack.Remove(e.Control);
        }

        base.OnControlRemoved(e);
        if (!arranging)
        {
            Relayout();
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Relayout();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollByWheel(e.Delta);
        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }
        base.OnMouseWheel(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
    }
}
