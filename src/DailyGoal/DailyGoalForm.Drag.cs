namespace DailyGoal;

/// <summary>
/// 窗口拖动与边缘吸附：展开态按住标题区即可拖动整窗，收起态按住贴条拖动；
/// 松手后按窗口可见部分所在的半屏自动识别最近的屏幕边，吸附并隐藏到那一侧。
/// </summary>
internal sealed partial class DailyGoalForm
{
    /// <summary>拖动过程中可见部分至少要留在虚拟桌面内的像素数，避免窗口被拖到看不见的位置。</summary>
    private const int DragKeepVisibleMargin = 48;

    private bool windowDragging;
    private bool windowDragMoved;
    private Point windowDragOrigin;
    private Point windowDragPointer;
    private Rectangle windowDragVisible;
    private Control? windowDragSource;

    /// <summary>
    /// 把一块区域变成窗口拖动面。showDragCursor 只给没有子控件的叶子控件打开，
    /// 因为 WinForms 的 Cursor 会被子控件继承，给容器设置会污染任务行等内部控件。
    /// </summary>
    private void AttachWindowDrag(Control control, bool showDragCursor = true)
    {
        control.MouseDown += WindowDragOnMouseDown;
        control.MouseMove += WindowDragOnMouseMove;
        control.MouseUp += WindowDragOnMouseUp;
        control.MouseCaptureChanged += WindowDragOnCaptureChanged;
        if (showDragCursor)
        {
            control.Cursor = Cursors.SizeAll;
        }
    }

    private void WindowDragOnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        windowDragSource = sender as Control;
        BeginWindowDrag(Cursor.Position);
        if (windowDragging && windowDragSource is { } source)
        {
            source.Capture = true;
        }
    }

    private void WindowDragOnMouseMove(object? sender, MouseEventArgs e)
    {
        if (windowDragging && e.Button == MouseButtons.Left)
        {
            UpdateWindowDrag(Cursor.Position);
        }
    }

    private void WindowDragOnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            EndWindowDrag();
        }
    }

    /// <summary>拖动中丢失鼠标捕获（例如被其他窗口抢走）时也要收尾，否则窗口会卡在拖动状态。</summary>
    private void WindowDragOnCaptureChanged(object? sender, EventArgs e)
    {
        if (windowDragging && sender is Control control && !control.Capture)
        {
            EndWindowDrag();
        }
    }

    /// <summary>当前状态下真正可见的窗口区域（窗体坐标）：展开态是整窗，收起态只有贴条。</summary>
    private Rectangle VisibleBounds()
    {
        if (targetWidth != CollapsedWidth)
        {
            return new Rectangle(0, 0, Width, Height);
        }

        return new Rectangle(
            slideUsesPosition && state.Settings.DockSide == DockSide.Left ? ExpandedWidth - CollapsedWidth : 0,
            0,
            CollapsedWidth,
            Height);
    }

    /// <summary>开始拖动整窗；贴条拖动与标题区拖动共用这一套逻辑。</summary>
    internal void BeginWindowDrag(Point screenPoint)
    {
        if (windowDragging || IsDisposed || Disposing)
        {
            return;
        }

        windowDragging = true;
        windowDragMoved = false;
        windowDragOrigin = Location;
        windowDragPointer = screenPoint;
        lastInside = DateTime.Now;
        peekHoverStart = null;
        // 拖动优先于收展动画：先把没跑完的滑动落到目标状态，位置再完全交给指针。
        if (slideTimer.Enabled)
        {
            FinishPendingSlide();
            windowDragOrigin = Location;
        }

        slideMode = SlideMode.StateChange;
        windowDragVisible = VisibleBounds();
    }

    internal void UpdateWindowDrag(Point screenPoint)
    {
        if (!windowDragging)
        {
            return;
        }

        var deltaX = screenPoint.X - windowDragPointer.X;
        var deltaY = screenPoint.Y - windowDragPointer.Y;
        if (!windowDragMoved
            && Math.Abs(deltaX) < SystemInformation.DragSize.Width
            && Math.Abs(deltaY) < SystemInformation.DragSize.Height)
        {
            return;
        }

        windowDragMoved = true;
        Location = ClampDragLocation(new Point(windowDragOrigin.X + deltaX, windowDragOrigin.Y + deltaY));
        lastInside = DateTime.Now;
    }

    internal void EndWindowDrag()
    {
        if (!windowDragging)
        {
            return;
        }

        windowDragging = false;
        if (windowDragSource is { } source && source.IsHandleCreated)
        {
            source.Capture = false;
        }

        windowDragSource = null;
        lastInside = DateTime.Now;
        peekHoverStart = null;
        if (!windowDragMoved)
        {
            return;
        }

        SnapToNearestEdge();
    }

    /// <summary>拖动时窗口跟随指针，同时保证可见部分不会离开虚拟桌面。</summary>
    private Point ClampDragLocation(Point location)
    {
        var virtualScreen = SystemInformation.VirtualScreen;
        var visible = windowDragVisible;
        var marginX = Math.Min(visible.Width, UiTheme.S(DragKeepVisibleMargin));
        var marginY = Math.Min(visible.Height, UiTheme.S(DragKeepVisibleMargin));
        var visibleLeft = location.X + visible.Left;
        var visibleTop = location.Y + visible.Top;

        // 横向按可见部分留边（收起态窗体有 558px 在屏幕外，不能用窗体左缘判断）；
        // 纵向整条可见区域都留在桌面内，保证标题区始终能再次抓到。
        var minLeft = virtualScreen.Left - visible.Width + marginX;
        var maxLeft = virtualScreen.Right - marginX;
        var minTop = virtualScreen.Top;
        var maxTop = Math.Max(minTop, virtualScreen.Bottom - visible.Height);
        var clampedLeft = Math.Clamp(visibleLeft, Math.Min(minLeft, maxLeft), Math.Max(minLeft, maxLeft));
        var clampedTop = Math.Clamp(visibleTop, minTop, maxTop);
        return new Point(
            location.X + (clampedLeft - visibleLeft),
            location.Y + (clampedTop - visibleTop));
    }

    /// <summary>松手后按可见部分所在的半屏识别最近的边，吸附到那一侧并记住纵向位置。</summary>
    private void SnapToNearestEdge()
    {
        var visible = VisibleBounds();
        var anchor = new Point(
            Left + visible.Left + (visible.Width / 2),
            Top + visible.Top + (visible.Height / 2));
        var screen = Screen.FromPoint(anchor) ?? Screen.PrimaryScreen;
        var area = screen?.WorkingArea ?? workArea;
        var side = anchor.X < area.Left + (area.Width / 2) ? DockSide.Left : DockSide.Right;
        var deviceName = screen is { Primary: false } ? screen.DeviceName : null;

        var previousSettings = state.Settings.Clone();
        var previousWorkArea = workArea;
        var previousSlideUsesPosition = slideUsesPosition;
        var previousRailPointsRight = peek.PointRight;
        var previousTop = Top;

        state.Settings.DockSide = side;
        state.Settings.PreferredScreenDevice = deviceName;
        workArea = area;
        state.Settings.WindowTopOffset = Math.Clamp(Top - workArea.Top, 0, Math.Max(0, workArea.Height - Height - 4));
        slideUsesPosition = CanSlideOffscreen();
        peek.PointRight = side == DockSide.Left;
        UpdatePeekRailPlacement();

        if (!SaveState())
        {
            state.Settings = previousSettings;
            workArea = previousWorkArea;
            slideUsesPosition = previousSlideUsesPosition;
            peek.PointRight = previousRailPointsRight;
            UpdatePeekRailPlacement();
            Top = previousTop;
            return;
        }

        var sideChanged = previousSettings.DockSide != side;
        var screenChanged = !string.Equals(previousSettings.PreferredScreenDevice, deviceName, StringComparison.OrdinalIgnoreCase);
        SyncDockSettingControls();
        AnimateToDock();

        // 收起态只有一条贴条可见，提示没地方显示，也不打扰。
        if (targetWidth != ExpandedWidth)
        {
            return;
        }

        if (sideChanged)
        {
            ShowTransientStatus($"已吸附到屏幕{(side == DockSide.Left ? "左" : "右")}侧");
        }
        else if (screenChanged)
        {
            ShowTransientStatus("已移动到所选显示器");
        }
    }

    /// <summary>拖动改过停靠边/显示器后同步设置页芯片，保持界面与设置一致。</summary>
    private void SyncDockSettingControls()
    {
        suppressSettingsEvents = true;
        try
        {
            dockChips.SelectedIndex = (int)state.Settings.DockSide;
            if (screenChips is not null && availableScreens.Length > 0)
            {
                screenChips.SelectedIndex = ResolvePreferredScreenIndex();
            }
        }
        finally
        {
            suppressSettingsEvents = false;
        }
    }
}
