namespace DailyGoal;

internal sealed partial class DailyGoalForm
{
    private const int SlideDurationMs = 220;

    /// <summary>滑动动画的两种用途：收展切换，以及拖动松手后滑向新的停靠边。</summary>
    private enum SlideMode
    {
        StateChange,
        DockMove
    }

    private DateTime lastCompletedAt = DateTime.MinValue;
    private int completionCombo;
    private bool particleTimerBoosted;
    private readonly System.Diagnostics.Stopwatch slideClock = new();
    private int slideFromWidth;
    private int slideFromLeft;
    private SlideMode slideMode = SlideMode.StateChange;
    private Point dockMoveFrom;
    private Point dockMoveTo;
    private bool fullscreenSuppressed;
    private bool fullscreenWasVisible;
    private bool fullscreenWasExpanded;

    private bool InteractionLocked => settingsVisible
        || templateView.Visible
        || recurringView.Visible
        || dragInProgress
        || windowDragging
        || moreMenu.Visible
        || taskMenu.Visible
        || templateMenu.Visible
        || (taskInput.ContainsFocus && taskInput.TextLength > 0)
        || taskRows.Values.Any(row => row.Editing);

    private void SetExpandedState(bool expanded)
    {
        if (!state.Settings.AutoHideEnabled)
        {
            expanded = true;
        }

        slideMode = SlideMode.StateChange;
        targetWidth = expanded ? ExpandedWidth : CollapsedWidth;
        if (expanded)
        {
            content.Visible = true;
            peek.Visible = false;
            content.BringToFront();
        }
        else
        {
            // Prepaint the rail behind the content so the final frame does not flash black.
            peek.Visible = true;
            peek.SendToBack();
            content.BringToFront();
        }

        if (state.Settings.ReducedMotion)
        {
            slideTimer.Stop();
            if (slideUsesPosition)
            {
                Width = ExpandedWidth;
                Left = SlidingDockedLeft(targetWidth);
            }
            else
            {
                Width = targetWidth;
                Left = DockedLeft(Width);
            }
            Top = WindowTop;
            CompleteSlideVisuals();
            return;
        }

        slideFromWidth = Width;
        slideFromLeft = Left;
        slideClock.Restart();
        slideTimer.Start();
    }

    private void TickSlide()
    {
        if (slideMode == SlideMode.DockMove)
        {
            TickDockMove();
            return;
        }

        var targetLeft = slideUsesPosition
            ? SlidingDockedLeft(targetWidth)
            : DockedLeft(targetWidth);
        var atTarget = slideUsesPosition
            ? Left == targetLeft
            : Width == targetWidth;
        if (atTarget)
        {
            slideTimer.Stop();
            CompleteSlideVisuals();
            return;
        }

        var progress = Math.Clamp(slideClock.Elapsed.TotalMilliseconds / SlideDurationMs, 0d, 1d);
        var eased = 1d - Math.Pow(1d - progress, 3);
        if (slideUsesPosition)
        {
            Left = progress >= 1d
                ? targetLeft
                : slideFromLeft + (int)Math.Round((targetLeft - slideFromLeft) * eased);
        }
        else
        {
            Width = progress >= 1d
                ? targetWidth
                : slideFromWidth + (int)Math.Round((targetWidth - slideFromWidth) * eased);
            Left = DockedLeft(Width);
        }

        if (progress >= 1d)
        {
            slideTimer.Stop();
            CompleteSlideVisuals();
        }
    }

    private void CompleteSlideVisuals()
    {
        if (targetWidth == CollapsedWidth)
        {
            content.Visible = false;
            peek.Visible = true;
            peek.BringToFront();
            return;
        }

        content.Visible = true;
        peek.Visible = false;
        content.BringToFront();
    }

    /// <summary>立刻结束进行中的收展动画，把窗口落到目标状态（拖动开始前、减少动态效果时用）。</summary>
    private void FinishPendingSlide()
    {
        slideTimer.Stop();
        slideMode = SlideMode.StateChange;
        Width = slideUsesPosition ? ExpandedWidth : targetWidth;
        Left = slideUsesPosition ? SlidingDockedLeft(targetWidth) : DockedLeft(targetWidth);
        Top = WindowTop;
        CompleteSlideVisuals();
        SyncParticleLayerPosition();
    }

    /// <summary>拖动结束后滑到新的停靠边：只挪位置，宽度与可见性保持不变。</summary>
    private void AnimateToDock()
    {
        // 拖动可能从中途打断收展动画，先把宽度归位到当前状态。
        Width = slideUsesPosition ? ExpandedWidth : targetWidth;
        if (state.Settings.ReducedMotion)
        {
            FinishPendingSlide();
            return;
        }

        slideMode = SlideMode.DockMove;
        dockMoveFrom = new Point(Left, Top);
        dockMoveTo = new Point(
            slideUsesPosition ? SlidingDockedLeft(targetWidth) : DockedLeft(targetWidth),
            WindowTop);
        slideClock.Restart();
        slideTimer.Start();
    }

    private void TickDockMove()
    {
        var progress = Math.Clamp(slideClock.Elapsed.TotalMilliseconds / SlideDurationMs, 0d, 1d);
        var eased = 1d - Math.Pow(1d - progress, 3);
        Left = progress >= 1d
            ? dockMoveTo.X
            : dockMoveFrom.X + (int)Math.Round((dockMoveTo.X - dockMoveFrom.X) * eased);
        Top = progress >= 1d
            ? dockMoveTo.Y
            : dockMoveFrom.Y + (int)Math.Round((dockMoveTo.Y - dockMoveFrom.Y) * eased);
        if (progress < 1d)
        {
            return;
        }

        slideTimer.Stop();
        slideMode = SlideMode.StateChange;
        CompleteSlideVisuals();
        SyncParticleLayerPosition();
    }

    private void TickHover()
    {
        if (UpdateFullscreenSuppression())
        {
            return;
        }

        if (windowDragging)
        {
            // 拖动中位置完全跟着指针走，既不展开也不收起。
            lastInside = DateTime.Now;
            return;
        }

        if (!state.Settings.AutoHideEnabled)
        {
            if (targetWidth != ExpandedWidth)
            {
                SetExpandedState(true);
            }
            return;
        }

        var inside = Bounds.Contains(Cursor.Position);
        if (inside)
        {
            lastInside = DateTime.Now;
            if (targetWidth != ExpandedWidth && !railDragging)
            {
                // 悬停意图延迟：短暂扫过贴条（例如去点后面窗口的关闭按钮）不展开。
                peekHoverStart ??= DateTime.Now;
                if ((DateTime.Now - peekHoverStart.Value).TotalMilliseconds >= PeekExpandIntentMs)
                {
                    SetExpandedState(true);
                }
            }
            else if (targetWidth == ExpandedWidth)
            {
                peekHoverStart = null;
            }
            return;
        }

        peekHoverStart = null;
        if (InteractionLocked)
        {
            return;
        }

        var idleMilliseconds = (DateTime.Now - lastInside).TotalMilliseconds;
        if (targetWidth == ExpandedWidth && idleMilliseconds > state.Settings.AutoHideDelayMs)
        {
            SetExpandedState(false);
        }
    }

    private bool UpdateFullscreenSuppression()
    {
        var fullscreen = FullscreenDetector.IsForegroundFullscreen(Handle, Bounds);
        if (fullscreen)
        {
            if (!fullscreenSuppressed && Visible)
            {
                fullscreenSuppressed = true;
                fullscreenWasVisible = true;
                fullscreenWasExpanded = targetWidth == ExpandedWidth;
                peekHoverStart = null;
                slideTimer.Stop();
                Hide();
            }

            return true;
        }

        if (!fullscreenSuppressed)
        {
            return false;
        }

        fullscreenSuppressed = false;
        if (!fullscreenWasVisible || IsDisposed || Disposing)
        {
            return false;
        }

        fullscreenWasVisible = false;
        suppressActivationOnShow = true;
        try
        {
            Show();
        }
        finally
        {
            suppressActivationOnShow = false;
        }
        SetExpandedState(fullscreenWasExpanded);
        lastInside = DateTime.Now;
        return false;
    }

    private void CheckDateRollover()
    {
        var today = clock.Today;
        UpdateDateText();
        if (today == currentDate)
        {
            return;
        }

        var snapshot = state.Clone();
        var previousDate = currentDate;
        currentDate = today;
        var changed = scheduleService.ApplyRollover(state, today);
        changed |= recurrenceService.EnsureTasksForDate(state, today, clock.Now);
        changed |= state.PruneHistory(today);
        changed |= state.Normalize(today);
        if (changed && !SaveState())
        {
            state = snapshot;
            currentDate = previousDate;
            return;
        }

        HideUndoBar();
        UpdateDateText();
        RenderAll();
    }

    private void LaunchParticles(TaskRowControl sourceControl, TaskRecord task)
    {
        if (state.Settings.ReducedMotion)
        {
            return;
        }

        var now = DateTime.Now;
        completionCombo = (now - lastCompletedAt).TotalMilliseconds < 1800 ? completionCombo + 1 : 1;
        lastCompletedAt = now;

        var baseCount = state.Settings.ParticleIntensity switch
        {
            ParticleIntensity.Light => 28,
            ParticleIntensity.Standard => 46,
            _ => 62
        };
        var count = Math.Min(76, (int)Math.Round(baseCount * (1 + Math.Min(completionCombo - 1, 3) * 0.1)));
        var speedBoost = state.Settings.ParticleIntensity switch
        {
            ParticleIntensity.Light => 5.8,
            ParticleIntensity.Standard => 7.0,
            _ => 8.1
        };

        var overflow = particles.Count + count - MaxActiveParticles;
        if (overflow > 0)
        {
            particles.RemoveRange(0, Math.Min(overflow, particles.Count));
        }

        var center = PointToClient(sourceControl.PointToScreen(TaskRowControl.CheckCenter));
        var palette = BuildTaskParticlePalette(task.AccentIndex);
        var shapes = Enum.GetValues<ParticleShape>();
        for (var index = 0; index < count; index += 1)
        {
            var accent = index % 6 == 0;
            var shape = accent && index % 12 == 0
                ? ParticleShape.Star
                : accent
                    ? ParticleShape.Spark
                    : random.NextDouble() < 0.7
                        ? ParticleShape.Dot
                        : shapes[random.Next(shapes.Length)];
            var angle = Math.PI * 2 * index / count + (random.NextDouble() - 0.5) * 0.78;
            var speed = 3.4 + random.NextDouble() * speedBoost;
            particles.Add(new Particle
            {
                Color = palette[index % palette.Length],
                Shape = shape,
                Size = accent ? 0.94f + (float)random.NextDouble() * 0.4f : 0.62f + (float)random.NextDouble() * 0.38f,
                X = center.X + random.Next(-4, 5),
                Y = center.Y + random.Next(-4, 5),
                Vx = Math.Cos(angle) * speed,
                Vy = Math.Sin(angle) * speed - (2.4 + random.NextDouble() * 1.7),
                Life = 700 + random.Next(0, 280),
                Rotation = random.NextDouble() * 360,
                Spin = -8 + random.NextDouble() * 16,
                Accent = accent,
                HasTrail = accent || random.NextDouble() > 0.42
            });
        }

        if (!particleTimer.Enabled)
        {
            particleClock.Restart();
            lastParticleElapsedMs = 0;
        }

        particleLayer.ShowFor(this);
        StartParticleTimer();
    }

    /// <summary>爆发期间把系统计时器精度提到 1ms，让 WinForms Timer 接近 60fps；结束立即恢复。</summary>
    private void StartParticleTimer()
    {
        if (!particleTimerBoosted)
        {
            particleTimerBoosted = NativeMethods.TimeBeginPeriod(1) == 0;
        }

        particleTimer.Start();
    }

    private void StopParticleTimer()
    {
        particleTimer.Stop();
        if (particleTimerBoosted)
        {
            _ = NativeMethods.TimeEndPeriod(1);
            particleTimerBoosted = false;
        }
    }

    private static Color[] BuildTaskParticlePalette(int accentIndex)
    {
        return
        [
            UiTheme.Accent(accentIndex),
            UiTheme.Accent(accentIndex + 1),
            UiTheme.Accent(accentIndex + 3),
            Color.White,
            UiTheme.Sun
        ];
    }

    private void TickParticles()
    {
        if (!particleClock.IsRunning)
        {
            particleClock.Restart();
            lastParticleElapsedMs = 0;
        }

        var elapsedNow = particleClock.Elapsed.TotalMilliseconds;
        var elapsedMilliseconds = lastParticleElapsedMs <= 0
            ? particleTimer.Interval
            : Math.Clamp(elapsedNow - lastParticleElapsedMs, 1, 100);
        lastParticleElapsedMs = elapsedNow;
        var frameScale = elapsedMilliseconds / 16.0;

        for (var index = particles.Count - 1; index >= 0; index -= 1)
        {
            var particle = particles[index];
            particle.Age += elapsedMilliseconds;
            if (particle.Age >= particle.Life)
            {
                particles.RemoveAt(index);
                continue;
            }

            var progress = Math.Clamp(particle.Age / particle.Life, 0, 1);
            particle.Vy += 0.2 * frameScale;
            particle.Vx *= Math.Pow(0.986, frameScale);
            particle.Vy *= Math.Pow(0.99, frameScale);
            particle.X += particle.Vx * frameScale;
            particle.Y += particle.Vy * frameScale;
            particle.Rotation += particle.Spin * frameScale;
            particle.Opacity = progress < 0.32
                ? 1f
                : (float)Math.Max(0, 1 - ((progress - 0.32) / 0.68));
            particle.VisualScale = particle.Size * (float)(progress < 0.22
                ? 0.5 + progress * 2.25
                : Math.Max(0.1, 1.08 - (progress - 0.22) * 1.36));
        }

        particleLayer.UpdateFor(this);
        if (particles.Count == 0)
        {
            StopParticleTimer();
            particleClock.Stop();
            lastParticleElapsedMs = 0;
            particleLayer.Hide();
        }
    }
}
