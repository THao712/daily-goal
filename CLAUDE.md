# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概览

Daily Goal 是一个贴在 Windows 屏幕右上角的日常任务清单。仓库包含两套实现：

- **桌面版（主产品）**：`src/DailyGoal/` 下的 C# WinForms 应用（net10.0-windows），发布产物放在 `app/DailyGoal.exe`。
- **Web 原型**：仓库根目录的 `index.html` + `app.js` + `styles.css`，纯静态页面，数据存 localStorage，直接用浏览器打开即可。

## 常用命令

本机没有系统级 .NET SDK（只有运行时），SDK 安装在用户目录：用 `~/.dotnet/dotnet.exe`（即 `C:\Users\tongh\.dotnet\dotnet.exe`）执行下面的命令。

```bash
# 构建桌面版（Release 是本仓库使用的配置）
dotnet build src/DailyGoal -c Release

# 发布自包含 win-x64 版本并用 Inno Setup 生成安装包
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1

# 运行自检（无头测试模式，结果写到 exe 同目录的报告文件）
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --self-test           # 核心逻辑，写 selftest.txt
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --particle-self-test  # 粒子系统，写 particle-selftest.txt
src/DailyGoal/bin/Release/net10.0-windows/DailyGoal.exe --ui-self-test        # UI 渲染+截图，写 ui-selftest.txt 和 ui-selftest/ 截图目录
```

测试约定：自检失败时进程退出码为 1，异常写入 `selftest-failure.txt`。自检使用临时数据目录（`%TEMP%\DailyGoalSelfTest-*`），不会碰真实用户数据。没有独立的测试框架，所有测试都在 `DailyGoalForm.Tests.cs`、`PlanningSelfTests.cs`、`ReliabilitySelfTests.cs` 里，通过上述命令行开关触发。测试模式界面缩放固定为 1.0；设置环境变量 `DAILYGOAL_UI_SCALE=1.5` 可在其他缩放下跑 UI 自检核对布局。

发布桌面版后需要把 `DailyGoal.exe`/`DailyGoal.dll` 等产物拷到 `app/` 目录（桌面快捷方式直接指向 `app/DailyGoal.exe`）。安装器脚本在 `installer/DailyGoal.iss`，自包含发布配置在 `src/DailyGoal/Properties/PublishProfiles/WinX64SelfContained.pubxml`，输出为 `dist/DailyGoal-Setup-2.5.0-x64.exe`。

## 桌面版架构

**入口与单实例**：`Program.cs` 用命名 Mutex + EventWaitHandle 保证单实例；重复启动会通过 activation event 唤醒已有窗口。测试模式（`--self-test` 等开关）绕过单实例检查。`Program.Main` 在创建任何控件前解析目标显示器并调用 `UiTheme.ApplyScale()` 敲定界面缩放（优先环境变量 `DAILYGOAL_UI_SCALE`，其次设置里的自适应/100/125/150；新安装默认自适应）。`UiScaleResolver` 根据显示器工作区高度和 52% 最大宽度限制计算安全比例，最低 68%，因此手动大比例也不会超出小屏工作区。字体和布局常量在控件构造时就被读取，运行中切换比例仍通过重启生效。

**主窗体是分部类**，按职责拆成多个文件，都属于 `DailyGoalForm`：
- `DailyGoalForm.cs` — 字段、窗口生命周期、贴边收展（ExpandedWidth=600 / CollapsedWidth=42，均乘全局缩放）、托盘、全局快捷键（`RegisterHotKey`，测试模式不注册）、傍晚提醒、停靠边/显示器解析、贴条拖动记忆位置、`UndoEntry` 定义（完成撤销 `UndoCompletion` / 删除撤销 `UndoDeletion`）
- `DailyGoalForm.Views.cs` — 控件树的构建（纯代码布局，无 Designer）；设置页是 `settingsScroll`（StackScrollPanel）内嵌 `settingsContent`，用 `settingsCursor` 顺序排行
- `DailyGoalForm.Tasks.cs` — 任务增删改、完成/撤销（删除也可撤销）、拖动排序、今日页底部「已完成」可折叠分组、粘贴多行批量建任务、streak 与热力图数据计算
- `DailyGoalForm.Planning.cs` — 今日/明日/历史页、结转、重复任务（含管理页：改规则/每周多选/暂停/删除系列）、模板、任务颜色菜单
- `DailyGoalForm.Animation.cs` — 展开/收起动画（虚拟桌面外缘使用固定宽度平移，避免逐帧 Resize；内侧停靠保留兼容路径）、悬停展开意图延迟（约 240ms 防误触）、同屏全屏窗口抑制与恢复、粒子触发。滑动动画有两种用途（`SlideMode`）：收展切换与拖动松手后的 `DockMove`（只挪位置不改宽度）
- `DailyGoalForm.Drag.cs` — 窗口拖动与边缘吸附：展开态按住标题区（`dragGrip`/标题/日期/进度环/标题区空白）拖动整窗，收起态按住贴条拖动，两者共用 `BeginWindowDrag`/`UpdateWindowDrag`/`EndWindowDrag`；拖动中 `VisibleBounds()` 决定"可见部分"（收起态只有贴条）用于夹取与判定，松手后按可见部分所在半屏识别最近的屏幕边，写入 `DockSide`/`PreferredScreenDevice`/`WindowTopOffset` 并滑过去。`TickHover` 在 `windowDragging` 期间不自动收展
- `WindowGrip.cs` — 标题区左侧的六点拖动抓手（展开态可拖动的视觉提示）
- `FullscreenDetector.cs` — 基于前台无边框窗口与显示器边界判断全屏；排除本进程、Windows 桌面和普通带标题栏最大化窗口
- `DailyGoalForm.Tests.cs` — 自检实现

**状态与持久化**（`AppState.cs`）：
- `AppState` 是 v5 状态格式；`StateStore` 负责加载/保存，自动从旧版数组、v2、v3、v4 迁移。`AppSettings.UiScalePercent=0` 表示按目标显示器自适应，`StateStore.PeekStartupDisplaySettings()` 在完整加载状态前只读取启动所需的缩放与显示器偏好。
- 保存采用临时文件原子替换 + `tasks.json.bak` 备份；损坏文件另存为 `tasks.corrupt-*.json`。
- 数据默认在 `%APPDATA%\DailyGoal\tasks.json`；用户可在设置里更改数据目录，指针文件固定写在 `%APPDATA%\DailyGoal\storage-location.txt`（`StateStore.ResolveDataDirectory` 解析，目录失效自动回退默认）。`BackupService.cs` 每天建一份快照到 `backups/`，保留 30 份。
- `DailyGoalForm` 构造函数接受 `dataDirectoryOverride` 参数，测试用它指向临时目录。

**日程逻辑**（`Scheduling.cs`）：`ScheduleService` 处理跨天结转（昨日未完成任务优先结转到今日），`RecurrenceService` 处理每日/工作日/每周任务的自动生成（每天只生成一次）。每周规则支持多选星期（`RecurringTask.WeeklyDays`，空则回退单日 `WeeklyDay` 兼容旧数据）。

**主题系统**（`Theme.cs`）：深色、浅色和玄翎三套主题。三套色板的数据集中在 `UiTheme.Palette` record（`PaletteFor(mode)` 可取任意主题的色板，设置页的主题色卡预览用它）。`UiTheme` 静态类持有当前色板（`Apply(AppThemeMode)` 切换，静态构造默认深色），集中定义所有颜色、字体、圆角刻度（RadiusSmall/Medium/Large，随缩放）和强调色板（Accents 数组 + `AccentNames` 中文名，任务按 AccentIndex 取色，颜色可在任务菜单里手选），另有 `CardShadow`/`AccentSoft`/`SelectionFill` 等语义槽位供控件取"配合主题微调"的颜色——新控件不要写字面量颜色，缺槽位就加一个。**全局缩放**：`UiTheme.Scale` 由启动时 `ApplyScale()` 敲定（运行期不变），布局常量一律写 `UiTheme.S(设计值)`（取整）或 `Sf()`（浮点），字体是属性、按缩放重建。主题选择存在 `AppSettings.Theme`，设置页色卡与「···」菜单的主题子菜单均可切换；运行时切换靠 `DailyGoalForm.SwitchPalette()`：旧色板→新色板逐控件映射 BackColor/ForeColor（`PaletteSnapshot()` 保证三套色板顺序一致），owner-draw 部分重绘时自动读新值。玄翎角色素材位于 `src/DailyGoal/assets/`：`xuanling-avatar.png` 用于标题徽章，`xuanling-scene.png` 用于空状态大图和 `TaskFlowPanel` 淡水印；发布时均复制到 `app/assets/`。`OnAccent()`（亮强调色上自动选深色文字）、`Blend()`、`DrawFocusRing()` 是通用工具；`PaintHelpers.DrawTopHighlight()` 画卡片顶部内高光（浅色模式自动跳过）。窗口圆角优先走 Windows 11 的 `NativeMethods.ApplyRoundedCorners()`（DWM 抗锯齿圆角+系统投影），失败时回退 `Region` 裁剪。`IconPainter` 用 GDI+ 矢量绘制所有图标（IconKind 枚举）。所有自定义控件（`TaskRowControl`、`IconButton`、`SegmentButton`、`ToggleSwitch` 等）取色都走 `UiTheme`，改配色优先改这一个文件（少数控件里还有配合深色微调的字面量颜色）。弹出菜单的深色由 `DarkMenuRenderer`/`DarkMenuColorTable` 负责，通过 `UiTheme.StyleMenu()` 应用到每个 `ContextMenuStrip`，并在 `Program.Main` 里设为 `ToolStripManager.Renderer` 全局默认。**弹窗与提示**：确认/多选对话框一律用自绘的 `AppDialog`（按钮文字直接表达动作，禁止再用系统 MessageBox），短暂提示用 `StatusToast`。注意 UI 自检的 `HasHealthyCapturedSurface` 会拒绝近黑像素（RGB 全部 < 22 视为截图失败），主题颜色不要暗于这个阈值。

**自定义控件**都是独立文件（`TaskFlowPanel`、`TaskRowControl`、`ParticleLayer`、`ProgressRing`、`PeekRail` 等），大量使用 owner-draw；拖动排序通过 `TaskDragGhost` + `TaskFlowPanel` 的事件（`TaskDragEventArgs`/`TaskReorderEventArgs`）协作实现。滚动一律用自绘的 `SlimScrollBar`（禁止原生滚动条）：`TaskFlowPanel` 内置它并支持底部 footer 区（已完成分组），任意控件的竖向堆叠滚动用 `StackScrollPanel`（历史列表、设置页都是）。通用小控件：`ChoiceChips`（分段单选）、`WeekdayPicker`（星期多选）、`ThemeChoiceControl`（主题色卡）、`CompletedSectionHeader`/`CompletedRowControl`（已完成分组）、`AppDialog`、`StatusToast`。`PeekRail` 支持按住拖动（纵向调贴边位置、横向换停靠边，松手后吸附，存 `WindowTopOffset`），停靠左缘时箭头自动镜像。

## Web 原型

无构建步骤、无依赖。`app.js` 管理任务的增删改与完成动画（DOM 直接操作），`styles.css` 用 CSS 自定义属性（`:root` 变量）定义配色。改配色优先改 `:root` 里的变量。
