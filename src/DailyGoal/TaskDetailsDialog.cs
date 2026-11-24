using System.Drawing.Drawing2D;

namespace DailyGoal;

/// <summary>
/// 主题一致的任务详情编辑弹窗：用于编辑单条任务的轻量备注与子步骤清单。
/// </summary>
internal sealed class TaskDetailsDialog : Form
{
    private readonly TextBox notesBox = new();
    private readonly TextBox newSubTaskBox = new();
    private readonly ListBox subTaskList = new();
    private readonly List<SubTaskItem> workingSubTasks;
    private bool dwmCornersApplied;
    private bool saved;

    public string? ResultNotes { get; private set; }
    public List<SubTaskItem>? ResultSubTasks { get; private set; }

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

    private TaskDetailsDialog(TaskRecord task)
    {
        workingSubTasks = (task.SubTasks ?? []).Select(sub => sub.Clone()).ToList();

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = UiTheme.WindowTop;
        Font = UiTheme.BodyFont;

        var width = UiTheme.S(486);
        var pad = UiTheme.S(24);
        var innerWidth = width - pad * 2;

        var title = new Label
        {
            Text = "备注与子步骤",
            Font = UiTheme.HeadingFont,
            ForeColor = UiTheme.Text,
            BackColor = UiTheme.WindowTop,
            Location = new Point(pad, UiTheme.S(20)),
            Size = new Size(innerWidth, UiTheme.S(28))
        };
        Controls.Add(title);

        var taskCaption = new Label
        {
            Text = task.Text,
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.WindowTop,
            Location = new Point(pad, UiTheme.S(50)),
            Size = new Size(innerWidth, UiTheme.S(24)),
            AutoEllipsis = true
        };
        Controls.Add(taskCaption);

        var notesHeading = new Label
        {
            Text = "备注",
            Font = UiTheme.SmallBoldFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.WindowTop,
            Location = new Point(pad, UiTheme.S(82)),
            Size = new Size(innerWidth, UiTheme.S(22))
        };
        Controls.Add(notesHeading);

        notesBox.Location = new Point(pad, UiTheme.S(108));
        notesBox.Size = new Size(innerWidth, UiTheme.S(78));
        notesBox.Multiline = true;
        notesBox.AcceptsReturn = true;
        notesBox.ScrollBars = ScrollBars.Vertical;
        notesBox.MaxLength = 800;
        notesBox.Font = UiTheme.BodyFont;
        notesBox.BackColor = UiTheme.Surface;
        notesBox.ForeColor = UiTheme.Text;
        notesBox.BorderStyle = BorderStyle.FixedSingle;
        notesBox.Text = task.Notes ?? "";
        Controls.Add(notesBox);

        var subHeading = new Label
        {
            Text = "子步骤（回车添加，双击勾选/取消）",
            Font = UiTheme.SmallBoldFont,
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.WindowTop,
            Location = new Point(pad, UiTheme.S(198)),
            Size = new Size(innerWidth, UiTheme.S(22))
        };
        Controls.Add(subHeading);

        newSubTaskBox.Location = new Point(pad, UiTheme.S(224));
        newSubTaskBox.Size = new Size(innerWidth - UiTheme.S(86), UiTheme.S(34));
        newSubTaskBox.Font = UiTheme.BodyFont;
        newSubTaskBox.BackColor = UiTheme.Surface;
        newSubTaskBox.ForeColor = UiTheme.Text;
        newSubTaskBox.BorderStyle = BorderStyle.FixedSingle;
        newSubTaskBox.MaxLength = 120;
        newSubTaskBox.PlaceholderText = "添加一个子步骤…";
        newSubTaskBox.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                args.SuppressKeyPress = true;
                AddSubTask();
            }
        };
        Controls.Add(newSubTaskBox);

        var addSubButton = new SegmentButton
        {
            Text = "添加",
            Active = true,
            Location = new Point(width - pad - UiTheme.S(78), UiTheme.S(222)),
            Size = new Size(UiTheme.S(78), UiTheme.S(38))
        };
        addSubButton.Click += (_, _) => AddSubTask();
        Controls.Add(addSubButton);

        subTaskList.Location = new Point(pad, UiTheme.S(268));
        subTaskList.Size = new Size(innerWidth, UiTheme.S(156));
        subTaskList.Font = UiTheme.BodyFont;
        subTaskList.BackColor = UiTheme.Surface;
        subTaskList.ForeColor = UiTheme.Text;
        subTaskList.BorderStyle = BorderStyle.FixedSingle;
        subTaskList.DrawMode = DrawMode.OwnerDrawFixed;
        subTaskList.ItemHeight = UiTheme.S(36);
        subTaskList.DrawItem += DrawSubTaskItem;
        subTaskList.HandleCreated += (_, _) =>
            NativeMethods.ApplyScrollBarTheme(subTaskList.Handle, UiTheme.Mode == AppThemeMode.Dark);
        subTaskList.DoubleClick += (_, _) => ToggleSelectedSubTask();
        subTaskList.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Space)
            {
                ToggleSelectedSubTask();
                args.Handled = true;
            }
            else if (args.KeyCode == Keys.Delete)
            {
                DeleteSelectedSubTask();
                args.Handled = true;
            }
        };
        Controls.Add(subTaskList);

        var buttonY = subTaskList.Bottom + UiTheme.S(16);
        var toggleButton = new SegmentButton
        {
            Text = "勾选/取消",
            Location = new Point(pad, buttonY),
            Size = new Size(UiTheme.S(102), UiTheme.S(38))
        };
        toggleButton.Click += (_, _) => ToggleSelectedSubTask();
        Controls.Add(toggleButton);

        var deleteSubButton = new SegmentButton
        {
            Text = "删除步骤",
            Location = new Point(pad + UiTheme.S(110), buttonY),
            Size = new Size(UiTheme.S(96), UiTheme.S(38))
        };
        deleteSubButton.Click += (_, _) => DeleteSelectedSubTask();
        Controls.Add(deleteSubButton);

        var saveButton = new SegmentButton
        {
            Text = "保存",
            Active = true,
            Location = new Point(width - pad - UiTheme.S(92), buttonY),
            Size = new Size(UiTheme.S(92), UiTheme.S(38))
        };
        saveButton.Click += (_, _) => CommitAndClose();
        Controls.Add(saveButton);

        var cancelButton = new SegmentButton
        {
            Text = "取消",
            Location = new Point(saveButton.Left - UiTheme.S(98), buttonY),
            Size = new Size(UiTheme.S(88), UiTheme.S(38))
        };
        cancelButton.Click += (_, _) => Close();
        Controls.Add(cancelButton);

        ClientSize = new Size(width, buttonY + UiTheme.S(58));
        RefreshSubTaskList();
    }

    public static bool TryEdit(IWin32Window? owner, TaskRecord task, out string? notes, out List<SubTaskItem>? subTasks)
    {
        using var dialog = new TaskDetailsDialog(task);
        dialog.PositionOver(owner);
        if (owner is null)
        {
            dialog.ShowDialog();
        }
        else
        {
            dialog.ShowDialog(owner);
        }

        notes = dialog.ResultNotes;
        subTasks = dialog.ResultSubTasks;
        return dialog.saved;
    }

    private void AddSubTask()
    {
        var text = newSubTaskBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            newSubTaskBox.Focus();
            return;
        }

        workingSubTasks.Add(new SubTaskItem { Text = text });
        newSubTaskBox.Clear();
        RefreshSubTaskList(workingSubTasks.Count - 1);
        newSubTaskBox.Focus();
    }

    private void ToggleSelectedSubTask()
    {
        var index = subTaskList.SelectedIndex;
        if (index < 0 || index >= workingSubTasks.Count)
        {
            return;
        }

        workingSubTasks[index].Completed = !workingSubTasks[index].Completed;
        RefreshSubTaskList(index);
    }

    private void DeleteSelectedSubTask()
    {
        var index = subTaskList.SelectedIndex;
        if (index < 0 || index >= workingSubTasks.Count)
        {
            return;
        }

        workingSubTasks.RemoveAt(index);
        RefreshSubTaskList(Math.Min(index, workingSubTasks.Count - 1));
    }

    private void RefreshSubTaskList(int selectIndex = -1)
    {
        subTaskList.BeginUpdate();
        subTaskList.Items.Clear();
        foreach (var item in workingSubTasks)
        {
            subTaskList.Items.Add(item);
        }
        subTaskList.EndUpdate();
        if (selectIndex >= 0 && selectIndex < subTaskList.Items.Count)
        {
            subTaskList.SelectedIndex = selectIndex;
        }
    }

    private void DrawSubTaskItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= subTaskList.Items.Count || subTaskList.Items[e.Index] is not SubTaskItem sub)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(selected ? UiTheme.SelectionFill : UiTheme.Surface))
        {
            e.Graphics.FillRectangle(bg, e.Bounds);
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var boxSize = UiTheme.S(16);
        var boxRect = new Rectangle(e.Bounds.Left + UiTheme.S(10), e.Bounds.Top + (e.Bounds.Height - boxSize) / 2, boxSize, boxSize);
        using (var boxPath = PaintHelpers.RoundRect(boxRect, UiTheme.Sf(4)))
        {
            using var boxFill = new SolidBrush(sub.Completed ? UiTheme.Mint : UiTheme.SurfaceMuted);
            e.Graphics.FillPath(boxFill, boxPath);
            using var boxPen = new Pen(sub.Completed ? UiTheme.Mint : UiTheme.BorderStrong, 1.1f);
            e.Graphics.DrawPath(boxPen, boxPath);
        }

        if (sub.Completed)
        {
            using var checkPen = new Pen(UiTheme.OnAccent(UiTheme.Mint), UiTheme.Sf(1.8f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            e.Graphics.DrawLines(checkPen,
            [
                new PointF(boxRect.Left + boxSize * 0.24f, boxRect.Top + boxSize * 0.52f),
                new PointF(boxRect.Left + boxSize * 0.44f, boxRect.Top + boxSize * 0.72f),
                new PointF(boxRect.Left + boxSize * 0.78f, boxRect.Top + boxSize * 0.30f)
            ]);
        }

        var textRect = new Rectangle(boxRect.Right + UiTheme.S(10), e.Bounds.Top, e.Bounds.Width - boxRect.Right - UiTheme.S(18), e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            sub.Text,
            UiTheme.BodyFont,
            textRect,
            sub.Completed ? UiTheme.TextFaint : UiTheme.Text,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
    }

    private void CommitAndClose()
    {
        var pendingText = newSubTaskBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(pendingText))
        {
            workingSubTasks.Add(new SubTaskItem { Text = pendingText });
        }

        var notes = notesBox.Text.Trim();
        ResultNotes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        ResultSubTasks = workingSubTasks.Count == 0 ? null : workingSubTasks.Select(sub => sub.Clone()).ToList();
        saved = true;
        Close();
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Enter)
        {
            CommitAndClose();
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
}
