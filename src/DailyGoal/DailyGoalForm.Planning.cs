using System.Drawing.Drawing2D;

namespace DailyGoal;

internal enum RecurringDeleteAction
{
    SkipOccurrence,
    StopSeries
}

internal sealed partial class DailyGoalForm
{
    private bool AddTaskToDate(string text, DateOnly date, int? accentIndex = null)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var snapshot = state.Clone();
        foreach (var active in state.TasksForDate(date))
        {
            active.SortOrder += 1;
        }

        var task = new TaskRecord
        {
            Id = TaskRecord.NewId(),
            Text = text,
            CreatedAt = clock.Now,
            Day = date,
            SortOrder = 0
        };
        task.AccentIndex = accentIndex ?? TaskRecord.AccentForId(task.Id);
        state.Tasks.Add(task);
        state.NormalizeSortOrders(date);
        if (!SaveState())
        {
            state = snapshot;
            return false;
        }

        InsertScheduledTaskRow(task);
        UpdateHeaderStats();
        return true;
    }

    private void ShowTaskMenu(TaskRecord task, TaskRowControl row)
    {
        taskMenu.Items.Clear();
        var edit = new ToolStripMenuItem("编辑任务");
        edit.Click += (_, _) => row.BeginEdit();
        taskMenu.Items.Add(edit);

        var colorItem = new ToolStripMenuItem("颜色");
        for (var index = 0; index < UiTheme.Accents.Length; index += 1)
        {
            var accentIndex = index;
            var item = new ToolStripMenuItem(UiTheme.AccentNames[index])
            {
                Checked = task.AccentIndex == index,
                Image = MenuSwatches.For(UiTheme.Accent(index)),
                ImageScaling = ToolStripItemImageScaling.None
            };
            item.Click += (_, _) => SetTaskAccent(task, accentIndex);
            colorItem.DropDownItems.Add(item);
        }
        taskMenu.Items.Add(colorItem);

        var recurring = task.RecurrenceId is null
            ? null
            : state.RecurringTasks.FirstOrDefault(candidate => candidate.Id == task.RecurrenceId);
        if (recurring is null)
        {
            var repeat = new ToolStripMenuItem("设为重复");
            AddRecurrenceItem(repeat, "每日", RecurrenceRule.Daily, task);
            AddRecurrenceItem(repeat, "工作日", RecurrenceRule.Weekdays, task);
            AddRecurrenceItem(repeat, $"每周{FormatWeekday(task.Day.DayOfWeek)}", RecurrenceRule.Weekly, task);
            taskMenu.Items.Add(repeat);
        }
        else
        {
            var manage = new ToolStripMenuItem($"重复：{FormatRecurrence(recurring)}（管理…）");
            manage.Click += (_, _) => ShowRecurringManager(recurring.Id);
            taskMenu.Items.Add(manage);
        }

        var saveTemplate = new ToolStripMenuItem("保存为常用模板");
        saveTemplate.Click += (_, _) => SaveAsTemplate(task);
        taskMenu.Items.Add(saveTemplate);
        taskMenu.Items.Add(new ToolStripSeparator());
        var delete = new ToolStripMenuItem(recurring is null ? "删除任务" : "删除重复任务");
        delete.Click += (_, _) => DeleteTaskWithRecurrence(task.Id);
        taskMenu.Items.Add(delete);
        taskMenu.Font = UiTheme.BodyFont;
        UiTheme.StyleMenu(taskMenu);
        taskMenu.Show(row, new Point(Math.Max(8, row.Width - UiTheme.S(190)), row.Height - UiTheme.S(12)));
    }

    private void SetTaskAccent(TaskRecord task, int accentIndex)
    {
        if (task.AccentIndex == accentIndex)
        {
            return;
        }

        var snapshot = state.Clone();
        var previous = task.AccentIndex;
        task.AccentIndex = accentIndex;
        RecurringTask? recurring = null;
        var previousRecurringAccent = -1;
        if (task.RecurrenceId is not null)
        {
            recurring = state.RecurringTasks.FirstOrDefault(candidate => candidate.Id == task.RecurrenceId);
            if (recurring is not null)
            {
                previousRecurringAccent = recurring.AccentIndex;
                recurring.AccentIndex = accentIndex;
            }
        }

        if (!SaveState())
        {
            state = snapshot;
            task.AccentIndex = previous;
            if (recurring is not null)
            {
                recurring.AccentIndex = previousRecurringAccent;
            }
            return;
        }

        if (taskRows.TryGetValue(task.Id, out var row))
        {
            row.Invalidate();
        }
    }

    private void AddRecurrenceItem(ToolStripMenuItem parent, string text, RecurrenceRule rule, TaskRecord task)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => SetRecurrence(task, rule);
        parent.DropDownItems.Add(item);
    }

    private void SetRecurrence(TaskRecord task, RecurrenceRule rule)
    {
        if (!task.IsActive || task.RecurrenceId is not null)
        {
            return;
        }

        var snapshot = state.Clone();
        var recurring = new RecurringTask
        {
            Id = $"recurrence-{Guid.NewGuid():N}",
            Text = task.Text,
            AccentIndex = task.AccentIndex,
            Rule = rule,
            WeeklyDay = task.Day.DayOfWeek,
            WeeklyDays = rule == RecurrenceRule.Weekly ? [task.Day.DayOfWeek] : null,
            CreatedAt = clock.Now,
            LastGeneratedDate = task.Day,
            Enabled = true
        };
        state.RecurringTasks.Add(recurring);
        task.RecurrenceId = recurring.Id;
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        ShowTransientStatus($"已设为{FormatRecurrence(recurring)}重复");
        RenderScheduledTasks();
    }

    private void DeleteTaskWithRecurrence(string taskId, RecurringDeleteAction? action = null)
    {
        var task = state.Tasks.FirstOrDefault(candidate => candidate.Id == taskId && candidate.IsActive);
        if (task is null)
        {
            return;
        }

        var recurring = task.RecurrenceId is null
            ? null
            : state.RecurringTasks.FirstOrDefault(candidate => candidate.Id == task.RecurrenceId && candidate.Enabled);
        if (recurring is null)
        {
            DeleteTask(taskId);
            return;
        }

        if (action is null)
        {
            if (suppressDialogs)
            {
                action = RecurringDeleteAction.SkipOccurrence;
            }
            else
            {
                action = AppDialog.Choose(
                    this,
                    "删除重复任务",
                    $"“{task.Text}”是{FormatRecurrence(recurring)}重复任务。",
                    ("仅跳过今天", DialogTone.Primary),
                    ("停止重复", DialogTone.Danger),
                    ("取消", DialogTone.Neutral)) switch
                {
                    0 => RecurringDeleteAction.SkipOccurrence,
                    1 => RecurringDeleteAction.StopSeries,
                    _ => null
                };
            }
        }
        if (action is null)
        {
            return;
        }

        var snapshot = state.Clone();
        var deleted = task.Clone();
        state.Tasks.Remove(task);
        if (action == RecurringDeleteAction.StopSeries)
        {
            recurring.Enabled = false;
        }
        state.NormalizeSortOrders(task.Day);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        ShowUndo(
            new UndoDeletion(deleted, action == RecurringDeleteAction.StopSeries ? recurring.Id : null),
            action == RecurringDeleteAction.StopSeries ? $"已停止重复“{deleted.Text}”" : $"已跳过今天的“{deleted.Text}”");
        RemoveScheduledTaskRow(task.Id);
        UpdateHeaderStats();
    }

    private void SaveAsTemplate(TaskRecord task)
    {
        var text = task.Text.Trim();
        if (state.Templates.Any(template => string.Equals(template.Text.Trim(), text, StringComparison.OrdinalIgnoreCase)))
        {
            ShowTransientStatus("这个常用任务已经保存过了");
            return;
        }

        var snapshot = state.Clone();
        state.Templates.Add(new TaskTemplate
        {
            Id = $"template-{Guid.NewGuid():N}",
            Text = text,
            AccentIndex = task.AccentIndex,
            SortOrder = state.Templates.Count
        });
        state.Normalize(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        ShowTransientStatus("已保存为常用模板");
    }

    private void ShowTemplateMenu()
    {
        templateMenu.Items.Clear();
        foreach (var template in state.Templates.OrderBy(template => template.SortOrder))
        {
            var item = new ToolStripMenuItem(template.Text);
            item.Click += (_, _) => AddTaskToDate(template.Text, SelectedDate, template.AccentIndex);
            templateMenu.Items.Add(item);
        }
        if (templateMenu.Items.Count == 0)
        {
            templateMenu.Items.Add(new ToolStripMenuItem("还没有常用模板") { Enabled = false });
        }
        templateMenu.Items.Add(new ToolStripSeparator());
        var manage = new ToolStripMenuItem("管理模板");
        manage.Click += (_, _) => ShowTemplateManager();
        templateMenu.Items.Add(manage);
        templateMenu.Font = UiTheme.BodyFont;
        UiTheme.StyleMenu(templateMenu);
        templateMenu.Show(templateButton, new Point(0, templateButton.Height + 3));
    }

    private void BuildTemplateView()
    {
        templateView.Location = Point.Empty;
        templateView.Size = new Size(ExpandedWidth, appHeight);
        templateView.BackColor = UiTheme.WindowTop;
        templateView.Visible = false;
        content.Controls.Add(templateView);

        ConfigureIconButton(templateBackButton, IconKind.Back, "返回", new Point(UiTheme.S(24), UiTheme.S(23)), new Size(UiTheme.S(42), UiTheme.S(42)));
        templateBackButton.Click += (_, _) => HideTemplateManager();
        templateView.Controls.Add(templateBackButton);
        var title = CreateLabel("常用模板", new Point(UiTheme.S(76), UiTheme.S(22)), new Size(UiTheme.S(300), UiTheme.S(40)), UiTheme.TitleFont, UiTheme.Text);
        templateView.Controls.Add(title);
        ConfigureIconButton(templateCloseButton, IconKind.Close, "关闭", new Point(ExpandedWidth - UiTheme.S(48), UiTheme.S(24)), new Size(UiTheme.S(40), UiTheme.S(40)));
        templateCloseButton.Tone = IconButtonTone.Danger;
        templateCloseButton.Click += (_, _) => Close();
        templateView.Controls.Add(templateCloseButton);

        var hint = CreateLabel("拖动模板调整顺序，双击或在下方修改名称", new Point(UiTheme.S(34), UiTheme.S(78)), new Size(UiTheme.S(520), UiTheme.S(28)), UiTheme.SmallFont, UiTheme.TextMuted);
        templateView.Controls.Add(hint);

        templateList.Location = new Point(UiTheme.S(34), UiTheme.S(116));
        templateList.Size = new Size(ExpandedWidth - UiTheme.S(68), Math.Max(UiTheme.S(230), appHeight - UiTheme.S(290)));
        templateList.Font = UiTheme.BodyFont;
        templateList.BackColor = UiTheme.WindowTop;
        templateList.BorderStyle = BorderStyle.None;
        templateList.DrawMode = DrawMode.OwnerDrawFixed;
        templateList.ItemHeight = UiTheme.S(48);
        templateList.AllowDrop = true;
        templateList.DrawItem += DrawTemplateItem;
        templateList.HandleCreated += (_, _) =>
            NativeMethods.ApplyScrollBarTheme(templateList.Handle, UiTheme.Mode == AppThemeMode.Dark);
        templateList.SelectedIndexChanged += (_, _) =>
        {
            templateEditor.Text = templateList.SelectedItem is TaskTemplate template ? template.Text : "";
        };
        templateList.DoubleClick += (_, _) => templateEditor.Focus();
        templateList.MouseDown += TemplateListOnMouseDown;
        templateList.MouseMove += TemplateListOnMouseMove;
        templateList.DragOver += TemplateListOnDragOver;
        templateList.DragDrop += TemplateListOnDragDrop;
        templateView.Controls.Add(templateList);

        templateEditor.Location = new Point(UiTheme.S(34), appHeight - UiTheme.S(150));
        templateEditor.Size = new Size(UiTheme.S(310), UiTheme.S(42));
        templateEditor.Font = UiTheme.BodyFont;
        templateEditor.BackColor = UiTheme.Surface;
        templateEditor.ForeColor = UiTheme.Text;
        templateEditor.BorderStyle = BorderStyle.FixedSingle;
        templateEditor.MaxLength = 160;
        templateEditor.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                args.SuppressKeyPress = true;
                RenameSelectedTemplate();
            }
        };
        templateView.Controls.Add(templateEditor);

        renameTemplateButton.Text = "重命名";
        renameTemplateButton.Location = new Point(UiTheme.S(356), appHeight - UiTheme.S(153));
        renameTemplateButton.Size = new Size(UiTheme.S(98), UiTheme.S(46));
        renameTemplateButton.Click += (_, _) => RenameSelectedTemplate();
        templateView.Controls.Add(renameTemplateButton);
        deleteTemplateButton.Text = "删除";
        deleteTemplateButton.Location = new Point(UiTheme.S(466), appHeight - UiTheme.S(153));
        deleteTemplateButton.Size = new Size(UiTheme.S(100), UiTheme.S(46));
        deleteTemplateButton.Click += (_, _) => DeleteSelectedTemplate();
        templateView.Controls.Add(deleteTemplateButton);
    }

    private void ShowTemplateManager()
    {
        if (settingsVisible)
        {
            HideSettings();
        }
        if (recurringView.Visible)
        {
            HideRecurringManager();
        }
        RefreshTemplateList();
        SetMainChromeVisible(false);
        templateView.Visible = true;
        templateView.BringToFront();
        HideUndoBar();
        SetExpandedState(true);
    }

    private void HideTemplateManager()
    {
        templateView.Visible = false;
        SetMainChromeVisible(true);
        undoBar.BringToFront();
    }

    private void RefreshTemplateList(string? selectedId = null)
    {
        templateList.BeginUpdate();
        templateList.Items.Clear();
        foreach (var template in state.Templates.OrderBy(template => template.SortOrder))
        {
            templateList.Items.Add(template);
        }
        templateList.EndUpdate();
        if (templateList.Items.Count > 0)
        {
            var index = selectedId is null
                ? 0
                : state.Templates.OrderBy(template => template.SortOrder).ToList().FindIndex(template => template.Id == selectedId);
            templateList.SelectedIndex = Math.Max(0, index);
        }
    }

    private void DrawTemplateItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= templateList.Items.Count || templateList.Items[e.Index] is not TaskTemplate template)
        {
            return;
        }
        using (var background = new SolidBrush(UiTheme.WindowTop))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        var selected = (e.State & DrawItemState.Selected) != 0;
        var bounds = Rectangle.Inflate(e.Bounds, -5, -4);
        using var path = PaintHelpers.RoundRect(bounds, UiTheme.Sf(7));
        using var fill = new SolidBrush(selected ? UiTheme.SelectionFill : UiTheme.Surface);
        e.Graphics.FillPath(fill, path);
        using var accent = new SolidBrush(UiTheme.Accent(template.AccentIndex));
        e.Graphics.FillRectangle(accent, bounds.Left + UiTheme.S(8), bounds.Top + UiTheme.S(8), UiTheme.S(4), bounds.Height - UiTheme.S(16));
        TextRenderer.DrawText(e.Graphics, template.Text, UiTheme.BodyFont, new Rectangle(bounds.Left + UiTheme.S(24), bounds.Top, bounds.Width - UiTheme.S(34), bounds.Height), UiTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void RenameSelectedTemplate()
    {
        if (templateList.SelectedItem is not TaskTemplate template)
        {
            return;
        }
        var text = templateEditor.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            templateEditor.Focus();
            return;
        }
        if (state.Templates.Any(candidate => candidate.Id != template.Id && string.Equals(candidate.Text, text, StringComparison.OrdinalIgnoreCase)))
        {
            ShowTransientStatus("已有同名模板");
            return;
        }
        var previous = template.Text;
        template.Text = text;
        if (!SaveState())
        {
            template.Text = previous;
            return;
        }
        RefreshTemplateList(template.Id);
    }

    private void DeleteSelectedTemplate()
    {
        if (templateList.SelectedItem is not TaskTemplate template)
        {
            return;
        }
        var snapshot = state.Clone();
        state.Templates.Remove(template);
        state.Normalize(currentDate);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }
        RefreshTemplateList();
    }

    private void TemplateListOnMouseDown(object? sender, MouseEventArgs e)
    {
        templateDragIndex = templateList.IndexFromPoint(e.Location);
    }

    private void TemplateListOnMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && templateDragIndex >= 0)
        {
            templateList.DoDragDrop(templateDragIndex, DragDropEffects.Move);
        }
    }

    private void TemplateListOnDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(typeof(int)) == true ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void TemplateListOnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(int)) is not int source || source < 0 || source >= templateList.Items.Count)
        {
            return;
        }
        var point = templateList.PointToClient(new Point(e.X, e.Y));
        var target = templateList.IndexFromPoint(point);
        if (target < 0)
        {
            target = templateList.Items.Count - 1;
        }
        if (source == target)
        {
            return;
        }
        var ordered = state.Templates.OrderBy(template => template.SortOrder).ToList();
        var moved = ordered[source];
        ordered.RemoveAt(source);
        ordered.Insert(target, moved);
        for (var index = 0; index < ordered.Count; index += 1)
        {
            ordered[index].SortOrder = index;
        }
        if (!SaveState())
        {
            return;
        }
        RefreshTemplateList(moved.Id);
    }

    // ---------- 重复任务管理 ----------

    private RecurringTask? SelectedRecurringSeries => recurringList.SelectedItem as RecurringTask;

    private void BuildRecurringView()
    {
        recurringView.Location = Point.Empty;
        recurringView.Size = new Size(ExpandedWidth, appHeight);
        recurringView.BackColor = UiTheme.WindowTop;
        recurringView.Visible = false;
        content.Controls.Add(recurringView);

        ConfigureIconButton(recurringBackButton, IconKind.Back, "返回", new Point(UiTheme.S(24), UiTheme.S(23)), new Size(UiTheme.S(42), UiTheme.S(42)));
        recurringBackButton.Click += (_, _) => HideRecurringManager();
        recurringView.Controls.Add(recurringBackButton);
        var title = CreateLabel("重复任务", new Point(UiTheme.S(76), UiTheme.S(22)), new Size(UiTheme.S(300), UiTheme.S(40)), UiTheme.TitleFont, UiTheme.Text);
        recurringView.Controls.Add(title);
        ConfigureIconButton(recurringCloseButton, IconKind.Close, "关闭", new Point(ExpandedWidth - UiTheme.S(48), UiTheme.S(24)), new Size(UiTheme.S(40), UiTheme.S(40)));
        recurringCloseButton.Tone = IconButtonTone.Danger;
        recurringCloseButton.Click += (_, _) => Close();
        recurringView.Controls.Add(recurringCloseButton);

        var hint = CreateLabel("选中一个系列后可修改规则、暂停或删除（对之后的日期生效）", new Point(UiTheme.S(34), UiTheme.S(78)), new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(28)), UiTheme.SmallFont, UiTheme.TextMuted);
        recurringView.Controls.Add(hint);

        recurringList.Location = new Point(UiTheme.S(34), UiTheme.S(116));
        recurringList.Size = new Size(ExpandedWidth - UiTheme.S(68), Math.Max(UiTheme.S(190), appHeight - UiTheme.S(370)));
        recurringList.Font = UiTheme.BodyFont;
        recurringList.BackColor = UiTheme.WindowTop;
        recurringList.BorderStyle = BorderStyle.None;
        recurringList.DrawMode = DrawMode.OwnerDrawFixed;
        recurringList.ItemHeight = UiTheme.S(54);
        recurringList.DrawItem += DrawRecurringItem;
        recurringList.HandleCreated += (_, _) =>
            NativeMethods.ApplyScrollBarTheme(recurringList.Handle, UiTheme.Mode == AppThemeMode.Dark);
        recurringList.SelectedIndexChanged += (_, _) => LoadRecurringEditor();
        recurringView.Controls.Add(recurringList);

        recurringRuleChips.Labels = ["每日", "工作日", "每周"];
        recurringRuleChips.Location = new Point(UiTheme.S(34), appHeight - UiTheme.S(236));
        recurringRuleChips.Size = new Size(UiTheme.S(300), UiTheme.S(42));
        recurringRuleChips.SelectedChanged += (_, _) => ApplyRecurringRuleChange();
        recurringView.Controls.Add(recurringRuleChips);

        recurringEnableLabel.Text = "启用";
        recurringEnableLabel.Location = new Point(ExpandedWidth - UiTheme.S(158), appHeight - UiTheme.S(230));
        recurringEnableLabel.Size = new Size(UiTheme.S(56), UiTheme.S(30));
        recurringEnableLabel.Font = UiTheme.BodyBoldFont;
        recurringEnableLabel.ForeColor = UiTheme.Text;
        recurringEnableLabel.BackColor = UiTheme.WindowTop;
        recurringView.Controls.Add(recurringEnableLabel);

        recurringEnableToggle.Location = new Point(ExpandedWidth - UiTheme.S(92), appHeight - UiTheme.S(233));
        recurringEnableToggle.CheckedChanged += (_, _) => ApplyRecurringEnabledChange();
        recurringView.Controls.Add(recurringEnableToggle);

        recurringWeekdays.Location = new Point(UiTheme.S(34), appHeight - UiTheme.S(182));
        recurringWeekdays.Size = new Size(UiTheme.S(392), UiTheme.S(42));
        recurringWeekdays.Visible = false;
        recurringWeekdays.SelectionChanged += (_, _) => ApplyRecurringRuleChange();
        recurringView.Controls.Add(recurringWeekdays);

        deleteRecurringButton.Text = "删除系列";
        deleteRecurringButton.Location = new Point(ExpandedWidth - UiTheme.S(148), appHeight - UiTheme.S(184));
        deleteRecurringButton.Size = new Size(UiTheme.S(114), UiTheme.S(46));
        deleteRecurringButton.Click += (_, _) => DeleteRecurringSeries();
        recurringView.Controls.Add(deleteRecurringButton);

        var footnote = CreateLabel(
            "删除系列后，已生成的任务会保留为普通任务。",
            new Point(UiTheme.S(34), appHeight - UiTheme.S(120)),
            new Size(ExpandedWidth - UiTheme.S(68), UiTheme.S(28)),
            UiTheme.SmallFont,
            UiTheme.TextFaint);
        recurringView.Controls.Add(footnote);
    }

    private void ShowRecurringManager(string? selectId)
    {
        if (settingsVisible)
        {
            HideSettings();
        }
        if (templateView.Visible)
        {
            HideTemplateManager();
        }
        RefreshRecurringList(selectId);
        SetMainChromeVisible(false);
        recurringView.Visible = true;
        recurringView.BringToFront();
        HideUndoBar();
        SetExpandedState(true);
    }

    private void HideRecurringManager()
    {
        recurringView.Visible = false;
        SetMainChromeVisible(true);
        undoBar.BringToFront();
    }

    private void RefreshRecurringList(string? selectId = null)
    {
        suppressRecurringEvents = true;
        recurringList.BeginUpdate();
        recurringList.Items.Clear();
        foreach (var recurring in state.RecurringTasks.OrderByDescending(candidate => candidate.Enabled).ThenBy(candidate => candidate.CreatedAt))
        {
            recurringList.Items.Add(recurring);
        }
        recurringList.EndUpdate();
        suppressRecurringEvents = false;
        if (recurringList.Items.Count > 0)
        {
            var index = 0;
            if (selectId is not null)
            {
                for (var candidate = 0; candidate < recurringList.Items.Count; candidate += 1)
                {
                    if (recurringList.Items[candidate] is RecurringTask series && series.Id == selectId)
                    {
                        index = candidate;
                        break;
                    }
                }
            }
            recurringList.SelectedIndex = index;
        }
        else
        {
            LoadRecurringEditor();
        }
    }

    private void DrawRecurringItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= recurringList.Items.Count || recurringList.Items[e.Index] is not RecurringTask recurring)
        {
            return;
        }
        using (var background = new SolidBrush(UiTheme.WindowTop))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }
        var selected = (e.State & DrawItemState.Selected) != 0;
        var bounds = Rectangle.Inflate(e.Bounds, -5, -4);
        using var path = PaintHelpers.RoundRect(bounds, UiTheme.Sf(7));
        using (var fill = new SolidBrush(selected ? UiTheme.SelectionFill : UiTheme.Surface))
        {
            e.Graphics.FillPath(fill, path);
        }

        var accentColor = recurring.Enabled ? UiTheme.Accent(recurring.AccentIndex) : UiTheme.TextFaint;
        using (var accent = new SolidBrush(accentColor))
        {
            e.Graphics.FillRectangle(accent, bounds.Left + UiTheme.S(8), bounds.Top + UiTheme.S(8), UiTheme.S(4), bounds.Height - UiTheme.S(16));
        }

        var ruleText = recurring.Enabled ? FormatRecurrence(recurring) : "已暂停";
        var ruleWidth = UiTheme.S(150);
        TextRenderer.DrawText(
            e.Graphics,
            recurring.Text,
            UiTheme.BodyFont,
            new Rectangle(bounds.Left + UiTheme.S(24), bounds.Top, bounds.Width - ruleWidth - UiTheme.S(36), bounds.Height),
            recurring.Enabled ? UiTheme.Text : UiTheme.TextFaint,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(
            e.Graphics,
            ruleText,
            UiTheme.SmallBoldFont,
            new Rectangle(bounds.Right - ruleWidth - UiTheme.S(10), bounds.Top, ruleWidth, bounds.Height),
            recurring.Enabled ? UiTheme.AccentText : UiTheme.TextFaint,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private void LoadRecurringEditor()
    {
        var series = SelectedRecurringSeries;
        suppressRecurringEvents = true;
        var hasSeries = series is not null;
        recurringRuleChips.Enabled = hasSeries;
        recurringEnableToggle.Enabled = hasSeries;
        deleteRecurringButton.Enabled = hasSeries;
        if (series is not null)
        {
            recurringRuleChips.SelectedIndex = (int)series.Rule;
            recurringEnableToggle.Checked = series.Enabled;
            recurringWeekdays.SetSelection(series.EffectiveWeeklyDays);
            recurringWeekdays.Visible = series.Rule == RecurrenceRule.Weekly;
        }
        else
        {
            recurringWeekdays.Visible = false;
        }
        suppressRecurringEvents = false;
    }

    private void ApplyRecurringRuleChange()
    {
        if (suppressRecurringEvents || SelectedRecurringSeries is not { } series)
        {
            return;
        }

        var snapshot = state.Clone();
        series.Rule = (RecurrenceRule)Math.Clamp(recurringRuleChips.SelectedIndex, 0, 2);
        if (series.Rule == RecurrenceRule.Weekly)
        {
            var days = recurringWeekdays.Selected
                .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
                .ToList();
            if (days.Count == 0)
            {
                days = [series.WeeklyDay];
            }
            series.WeeklyDays = days;
            series.WeeklyDay = days[0];
        }

        var generated = series.Enabled && recurrenceService.EnsureTasksForDate(state, currentDate, clock.Now);
        if (!SaveState())
        {
            state = snapshot;
            LoadRecurringEditor();
            return;
        }

        recurringWeekdays.Visible = series.Rule == RecurrenceRule.Weekly;
        if (series.Rule == RecurrenceRule.Weekly)
        {
            suppressRecurringEvents = true;
            recurringWeekdays.SetSelection(series.EffectiveWeeklyDays);
            suppressRecurringEvents = false;
        }
        RefreshRecurringList(series.Id);
        if (generated)
        {
            RenderScheduledTasks();
        }
        ShowTransientStatus($"已改为{FormatRecurrence(series)}重复");
    }

    private void ApplyRecurringEnabledChange()
    {
        if (suppressRecurringEvents || SelectedRecurringSeries is not { } series)
        {
            return;
        }

        var snapshot = state.Clone();
        series.Enabled = recurringEnableToggle.Checked;
        var generated = series.Enabled && recurrenceService.EnsureTasksForDate(state, currentDate, clock.Now);
        if (!SaveState())
        {
            state = snapshot;
            LoadRecurringEditor();
            return;
        }

        RefreshRecurringList(series.Id);
        if (generated)
        {
            RenderScheduledTasks();
        }
        ShowTransientStatus(series.Enabled ? "已恢复该重复系列" : "已暂停该重复系列");
    }

    private void DeleteRecurringSeries()
    {
        if (SelectedRecurringSeries is not { } series)
        {
            return;
        }

        if (!ConfirmAction("删除重复系列", $"“{series.Text}”将不再自动出现。已生成的任务会保留为普通任务。", "删除"))
        {
            return;
        }

        var snapshot = state.Clone();
        foreach (var task in state.Tasks.Where(candidate => candidate.RecurrenceId == series.Id))
        {
            task.RecurrenceId = null;
        }
        state.RecurringTasks.Remove(series);
        if (!SaveState())
        {
            state = snapshot;
            return;
        }

        RefreshRecurringList();
        RenderScheduledTasks();
        ShowTransientStatus("已删除重复系列");
    }

    private static string FormatRecurrence(RecurringTask recurring)
    {
        return recurring.Rule switch
        {
            RecurrenceRule.Daily => "每日",
            RecurrenceRule.Weekdays => "工作日",
            RecurrenceRule.Weekly => $"每周{string.Join("·", recurring.EffectiveWeeklyDays.Select(FormatWeekday))}",
            _ => "每日"
        };
    }

    private static string FormatWeekday(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => "一",
            DayOfWeek.Tuesday => "二",
            DayOfWeek.Wednesday => "三",
            DayOfWeek.Thursday => "四",
            DayOfWeek.Friday => "五",
            DayOfWeek.Saturday => "六",
            _ => "日"
        };
    }
}

/// <summary>颜色菜单的小圆点图标缓存（按 ARGB 复用，主题切换后自然生成新色）。</summary>
internal static class MenuSwatches
{
    private static readonly Dictionary<int, Bitmap> Cache = [];

    public static Bitmap For(Color color)
    {
        var key = color.ToArgb();
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var size = UiTheme.S(14);
        var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            graphics.FillEllipse(brush, 1, 1, size - 2, size - 2);
        }

        Cache[key] = bitmap;
        return bitmap;
    }
}
