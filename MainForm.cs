using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Share;
using CountTimer.View;
using Microsoft.Win32;

namespace CountTimer
{
    public partial class MainForm : Window, AntdUI.IEventListener
    {
        private const int HotkeyMessage = 0x0312;
        private const int HotkeyId = 0xB0F1;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModNoRepeat = 0x4000;
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "CountTimer";
        // 窗口至少要有这么大一块落在某块屏幕的工作区内，才认为记录的位置还有效
        private const int MinVisibleWidth = 200;
        private const int MinVisibleHeight = 120;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private bool _isExitRequested = false;
        private readonly AppSettingsService settingsService = new AppSettingsService();
        // 窗口移动/缩放停下来之后再写配置，避免拖动过程中反复落盘
        private readonly System.Windows.Forms.Timer boundsSaveTimer = new() { Interval = 800 };
        private AppSettings appSettings;
        private PomodoroControl? pomodoroControl;
        private MemoControl? memoControl;
        private HolidayControl? holidayControl;
        private readonly bool startMinimized = Environment.GetCommandLineArgs()
            .Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        // 托盘图标：默认图标来自资源、不释放；时间图标是生成的，换掉后要释放
        private Icon? trayDefaultIcon;
        private Icon? trayTimeIcon;
        private string? trayStateKey;
        private TimeSpan trayRemaining;
        private bool trayRunning;
        private bool trayActive;
        private string trayPhase = string.Empty;
        private bool hotkeyRegistered;
        // 最近一次窗口以正常状态显示时的位置与大小；从未正常显示过（静默启动）时为 null
        private Rectangle? lastNormalBounds;

        public MainForm()
        {
            // 尽早应用已保存的主题与语言，避免启动时闪白（AntdUI 控件与自绘控件均跟随此配置）
            appSettings = settingsService.Load();
            AntdUI.Config.Mode = appSettings.DarkMode ? TMode.Dark : TMode.Light;
            I18n.Init(appSettings.Language == "en" ? AppLanguage.English : AppLanguage.Chinese);

            InitializeComponent();
            this.TopMost = appSettings.AlwaysOnTop;
            trayDefaultIcon = notifyIcon_cd.Icon;

            pomodoroControl = new PomodoroControl(ShowBalloon, appSettings);
            memoControl = new MemoControl();
            holidayControl = new HolidayControl();
            // 番茄钟页的主题开关 → 全局应用（EventHub 广播，所有 AntdUI 控件和自绘监听者自动换色）
            pomodoroControl.ThemeChanged += dark => AntdUI.Config.Mode = dark ? TMode.Dark : TMode.Light;
            pomodoroControl.TrayStateChanged += UpdateTrayIcon;
            pomodoroControl.AppSettingsChanged += ApplyAppSettings;

            tabPomodoro.Controls.Add(pomodoroControl);
            tabMemo.Controls.Add(memoControl);
            tabHoliday.Controls.Add(holidayControl);

            ApplyContainerTheme();
            I18n.Changed += ApplyLanguage;
            ApplyLanguage();
            ApplyAppSettings();
            this.AddListener();

            // 任务栏明暗跟随系统主题（与本应用的深色模式无关），托盘文字颜色要跟着换
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            boundsSaveTimer.Tick += (s, e) => SaveWindowBounds();
        }

        /// <summary>
        /// AntdUI 1.9 的 Config.Mode 只主题化 AntdUI 自绘控件，
        /// 窗体/Tabs 页/PageHeader 是普通 WinForms 表面，背景需按色板显式设置
        /// </summary>
        private void ApplyContainerTheme()
        {
            Color bgColor = AntdUI.Colour.BgLayout.Get();
            BackColor = bgColor;
            pageHeader1.BackColor = bgColor;
            tabPomodoro.BackColor = bgColor;
            tabMemo.BackColor = bgColor;
            tabHoliday.BackColor = bgColor;
            // 系统标题栏沉浸式暗色（Dark 属性由 AntdUI 内部处理）
            Dark = AntdUI.Config.Mode == TMode.Dark;
            Invalidate(true);
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                ApplyContainerTheme();
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        /// <summary>语言切换：窗体自身文案（标题/页签/托盘/菜单）+ 各页签 ApplyLanguage</summary>
        private void ApplyLanguage()
        {
            string title = I18n.T("番茄倒计时", "Pomodoro Countdown");
            Text = title;
            pageHeader1.Text = title;
            tabPomodoro.Text = I18n.T("番茄钟", "Pomodoro");
            tabMemo.Text = I18n.T("小计", "Notes");
            tabHoliday.Text = I18n.T("节假日", "Holidays");
            showWindowToolStripMenuItem.Text = I18n.T("显示主界面", "Show");
            exitToolStripMenuItem.Text = I18n.T("退出", "Exit");
            UpdateTrayTooltip();

            pomodoroControl?.ApplyLanguage();
            memoControl?.ApplyLanguage();
            holidayControl?.ApplyLanguage();
        }

        #region 托盘

        /// <summary>统一的托盘气泡通知入口</summary>
        public void ShowBalloon(string title, string text)
        {
            notifyIcon_cd.BalloonTipTitle = title;
            notifyIcon_cd.BalloonTipText = text;
            notifyIcon_cd.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon_cd.Visible = true;
            notifyIcon_cd.ShowBalloonTip(5000);
        }

        private void UpdateTrayIcon(TimeSpan remaining, bool running, bool active, string phase)
        {
            trayRemaining = remaining;
            trayRunning = running;
            trayActive = active;
            trayPhase = phase;
            RefreshTray(force: false);
        }

        /// <summary>托盘图标：有进行中的番茄钟阶段时画剩余时间（暂停时变暗），否则用应用图标</summary>
        private void RefreshTray(bool force)
        {
            bool showTime = trayActive && appSettings.ShowTrayTime;
            string? text = showTime ? TrayTimeIcon.Format(trayRemaining) : null;
            // 计时每 250ms 触发一次，文字没变就不重建图标
            string key = $"{text}|{showTime && !trayRunning}";
            if (!force && key == trayStateKey)
            {
                return;
            }

            trayStateKey = key;
            Icon? previous = trayTimeIcon;
            trayTimeIcon = null;
            if (text is null)
            {
                notifyIcon_cd.Icon = trayDefaultIcon;
            }
            else
            {
                trayTimeIcon = TrayTimeIcon.Create(text, paused: !trayRunning, TrayTimeIcon.TraySize, TrayTimeIcon.IsTaskbarLight());
                notifyIcon_cd.Icon = trayTimeIcon;
            }

            previous?.Dispose();
            UpdateTrayTooltip();
        }

        private void UpdateTrayTooltip()
        {
            if (!trayActive)
            {
                notifyIcon_cd.Text = I18n.T("番茄倒计时", "Pomodoro Countdown");
                return;
            }

            string time = TrayTimeIcon.Format(trayRemaining);
            notifyIcon_cd.Text = trayRunning
                ? I18n.F("{0} · {1}", "{0} · {1}", trayPhase, time)
                : I18n.F("{0} · 已暂停 {1}", "{0} · Paused {1}", trayPhase, time);
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(() => RefreshTray(force: true));
            }
        }

        #endregion

        #region 应用级设置：置顶 / 开机自启 / 全局快捷键

        private void ApplyAppSettings()
        {
            TopMost = appSettings.AlwaysOnTop;
            ApplyStartWithWindows();
            ApplyHotkey();
            RefreshTray(force: true);
        }

        private void ApplyStartWithWindows()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                if (key is null)
                {
                    return;
                }

                if (appSettings.StartWithWindows)
                {
                    key.SetValue(RunValueName, StartCommand(), RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(RunValueName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                ShowError(I18n.T("设置开机自启失败", "Could not change auto start"), I18n.T("写入注册表被拒绝", "Registry access was denied"));
            }
        }

        /// <summary>自启命令行：带 --minimized，登录后直接进托盘不弹窗。用 dotnet 宿主启动时要把 dll 带上</summary>
        private static string StartCommand()
        {
            string executable = Application.ExecutablePath;
            if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase)
                && Assembly.GetEntryAssembly()?.Location is { Length: > 0 } entryAssembly)
            {
                return $"\"{executable}\" \"{entryAssembly}\" --minimized";
            }

            return $"\"{executable}\" --minimized";
        }

        /// <summary>注册开始/暂停的全局快捷键；组合被占用时提示但不影响其他功能</summary>
        private void ApplyHotkey()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            if (hotkeyRegistered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                hotkeyRegistered = false;
            }

            if (!Hotkey.TryParse(appSettings.Hotkey, out Hotkey hotkey) || !hotkey.IsValid)
            {
                return;
            }

            uint modifiers = ModNoRepeat
                | (hotkey.Ctrl ? ModControl : 0)
                | (hotkey.Alt ? ModAlt : 0)
                | (hotkey.Shift ? ModShift : 0);
            if (RegisterHotKey(Handle, HotkeyId, modifiers, (uint)hotkey.Key))
            {
                hotkeyRegistered = true;
            }
            else
            {
                ShowError(
                    I18n.F("快捷键 {0} 已被其他程序占用", "Hotkey {0} is taken by another app", hotkey),
                    I18n.T("换一个组合即可", "Try another combination"));
            }
        }

        private void ShowError(string message, string detail)
        {
            if (IsHandleCreated)
            {
                AntdUI.Message.error(this, $"{message}：{detail}", autoClose: 3);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyHotkey();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (hotkeyRegistered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                hotkeyRegistered = false;
            }

            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == HotkeyMessage && m.WParam.ToInt32() == HotkeyId)
            {
                pomodoroControl?.ToggleFromHotkey();
                return;
            }

            base.WndProc(ref m);
        }

        #endregion

        #region 窗口显示与位置

        /// <summary>
        /// 自定义方法：窗体的隐藏与显示
        /// </summary>
        /// <param name="display"></param>
        private void windowDisplay(bool display)
        {
            if (display)
            {
                this.WindowState = FormWindowState.Normal; // 窗口常规化
                this.ShowInTaskbar = true; // 显示在任务栏
            }
            else
            {
                SaveWindowBounds();
                this.WindowState = FormWindowState.Minimized; // 窗口最小化
                this.ShowInTaskbar = false; // 不显示在任务栏
            }

        }

        // 在托盘图标右键点菜单“显示主界面”时显示窗体
        private void showWindowMenuItem_Click(object sender, EventArgs e)
        {
            windowDisplay(true);
        }

        // 在托盘图标右键点菜单“退出”时退出程序
        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            _isExitRequested = true;
            Application.Exit();
        }

        // 点击托盘图标显示出窗体
        private void notifyIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) // 点击左键才弹出
            {
                windowDisplay(true);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 系统关机/重启时不阻止
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                return;
            }

            // 用户主动关闭时隐藏窗口
            if (!_isExitRequested && e.CloseReason == CloseReason.UserClosing)
            {
                windowDisplay(false);
                e.Cancel = true;
            }

        }

        /// <summary>回到上次的位置和大小；没记录过，或那块屏幕已经不在了，就回到右下角</summary>
        private void RestoreWindowBounds()
        {
            // 设计器里默认居中，这里改为手动定位，否则下面设置的 Bounds 会被居中逻辑覆盖
            this.StartPosition = FormStartPosition.Manual;
            var saved = new Rectangle(appSettings.WindowX, appSettings.WindowY, appSettings.WindowWidth, appSettings.WindowHeight);
            if (saved.Width >= MinimumSize.Width && saved.Height >= MinimumSize.Height && IsUsable(saved))
            {
                Bounds = saved;
                return;
            }

            // 获取主显示器工作区域（排除任务栏）
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);

            // 计算右下角坐标
            this.Location = new Point(workingArea.Right - this.Width, workingArea.Bottom - this.Height);
        }

        private static bool IsUsable(Rectangle bounds)
        {
            return Screen.AllScreens.Any(screen =>
            {
                var visible = screen.WorkingArea;
                visible.Intersect(bounds);
                return visible.Width >= MinVisibleWidth && visible.Height >= MinVisibleHeight;
            });
        }

        private void SaveWindowBounds()
        {
            boundsSaveTimer.Stop();
            // 只记录窗口确实以正常状态显示过的位置：静默启动后还没显示过时，.NET 给出的"恢复位置"
            // 宽高是最小尺寸，写进去会让窗口一次次缩小，所以这种情况保留已有的值
            if (WindowState == FormWindowState.Normal)
            {
                lastNormalBounds = Bounds;
            }

            if (lastNormalBounds is not Rectangle bounds || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            appSettings.WindowX = bounds.X;
            appSettings.WindowY = bounds.Y;
            appSettings.WindowWidth = bounds.Width;
            appSettings.WindowHeight = bounds.Height;
            try
            {
                settingsService.Save(appSettings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            RestoreWindowBounds();
            Move += (s, a) => ScheduleBoundsSave();
            ResizeEnd += (s, a) => ScheduleBoundsSave();

            if (startMinimized)
            {
                windowDisplay(false);
            }

            // E2E 自测模式：CT_E2E=1 时自动回归"开始计时→切页签→计时存活"
            if (Environment.GetEnvironmentVariable("CT_E2E") == "1")
            {
                _ = RunE2eSelfTest();
            }
        }

        private void ScheduleBoundsSave()
        {
            boundsSaveTimer.Stop();
            boundsSaveTimer.Start();
        }

        /// <summary>E2E 自测用：按系统投递快捷键的同一条消息路径触发一次</summary>
        internal void PressHotkeyForTest()
        {
            var message = System.Windows.Forms.Message.Create(Handle, HotkeyMessage, (IntPtr)HotkeyId, IntPtr.Zero);
            WndProc(ref message);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            notifyIcon_cd.Visible = false;
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            I18n.Changed -= ApplyLanguage;
            boundsSaveTimer.Dispose();
            trayTimeIcon?.Dispose();
            trayTimeIcon = null;
            base.OnFormClosed(e);
        }

        #endregion

        private async System.Threading.Tasks.Task RunE2eSelfTest()
        {
            string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ct_e2e_result.log");
            System.IO.File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] e2e begin\n");
            // 静默启动时窗口还没显示过，这里验证从托盘调出后位置和尺寸恢复正确
            if (startMinimized)
            {
                System.IO.File.AppendAllText(logPath, $"silent start: state={WindowState} restore={RestoreBounds}\n");
                windowDisplay(true);
                await System.Threading.Tasks.Task.Delay(800);
                System.IO.File.AppendAllText(logPath, $"after show from tray: state={WindowState} bounds={Bounds}\n");
            }
            await System.Threading.Tasks.Task.Delay(1200);
            System.IO.File.AppendAllText(logPath, $"initial running={pomodoroControl?.E2EIsRunning}\n");

            pomodoroControl?.E2EStart();
            await System.Threading.Tasks.Task.Delay(1200);
            System.IO.File.AppendAllText(logPath, $"after start running={pomodoroControl?.E2EIsRunning} (expect True)\n");

            tabs.SelectedIndex = 1;
            await System.Threading.Tasks.Task.Delay(1200);
            System.IO.File.AppendAllText(logPath, $"after tab2 running={pomodoroControl?.E2EIsRunning} (expect True)\n");

            tabs.SelectedIndex = 0;
            await System.Threading.Tasks.Task.Delay(1200);
            System.IO.File.AppendAllText(logPath, $"after tab1 running={pomodoroControl?.E2EIsRunning} (expect True)\n");

            // 圆环必须是正方形（防字体自动缩放压扁）
            var ringSize = pomodoroControl?.E2ERingSize ?? System.Drawing.Size.Empty;
            System.IO.File.AppendAllText(logPath, $"ring size: {ringSize.Width}x{ringSize.Height} (expect Width==Height)\n");

            // 全局快捷键：系统级注册是否成功单独记录；这里向自己投递快捷键消息，验证处理路径（开始↔暂停）
            System.IO.File.AppendAllText(logPath, $"hotkey registered={hotkeyRegistered} (expect True)\n");
            PressHotkeyForTest();
            await System.Threading.Tasks.Task.Delay(600);
            System.IO.File.AppendAllText(logPath, $"after hotkey running={pomodoroControl?.E2EIsRunning} (expect False)\n");
            PressHotkeyForTest();
            await System.Threading.Tasks.Task.Delay(600);
            System.IO.File.AppendAllText(logPath, $"after hotkey again running={pomodoroControl?.E2EIsRunning} (expect True)\n");

            // 事件编辑抽屉：打开 → 出现新窗体 → 关闭 → 窗体消失
            int formsBefore = Application.OpenForms.Count;
            pomodoroControl?.E2EOpenDrawer();
            await System.Threading.Tasks.Task.Delay(1500);
            int formsDuring = Application.OpenForms.Count;
            pomodoroControl?.E2ECloseDrawer();
            await System.Threading.Tasks.Task.Delay(800);
            int formsAfter = Application.OpenForms.Count;
            System.IO.File.AppendAllText(logPath, $"drawer forms before={formsBefore} during={formsDuring} after800ms={formsAfter}\n");

            // 二次确认：再等 2.5 秒（阴影/遮罩层异步回收），并记录剩余窗体类型
            await System.Threading.Tasks.Task.Delay(2500);
            var remain = Application.OpenForms.Cast<Form>().Select(f => $"{f.GetType().Name}(visible={f.Visible})").ToArray();
            System.IO.File.AppendAllText(logPath, $"after3300ms count={remain.Length}: {string.Join(", ", remain)}\n");
            System.IO.File.AppendAllText(logPath, "e2e done\n");
        }
    }
}
