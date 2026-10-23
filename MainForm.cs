using System;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Service;
using CountTimer.View;

namespace CountTimer
{
    public partial class MainForm : Window, AntdUI.IEventListener
    {
        private bool _isExitRequested = false;
        private readonly AppSettingsService settingsService = new AppSettingsService();
        private PomodoroControl? pomodoroControl;
        private MemoControl? memoControl;

        public MainForm()
        {
            // 尽早应用已保存的主题，避免启动时闪白（AntdUI 控件与自绘控件均跟随此配置）
            bool darkMode = settingsService.Load().DarkMode;
            AntdUI.Config.Mode = darkMode ? TMode.Dark : TMode.Light;

            InitializeComponent();
            this.TopMost = true;

            pomodoroControl = new PomodoroControl(ShowBalloon);
            memoControl = new MemoControl();
            // 番茄钟页的主题开关 → 全局应用（EventHub 广播，所有 AntdUI 控件和自绘监听者自动换色）
            pomodoroControl.ThemeChanged += dark => AntdUI.Config.Mode = dark ? TMode.Dark : TMode.Light;

            tabPomodoro.Controls.Add(pomodoroControl);
            tabMemo.Controls.Add(memoControl);

            ApplyContainerTheme();
            this.AddListener();
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

        /// <summary>
        /// 统一的托盘气泡通知入口
        /// </summary>
        public void ShowBalloon(string title, string text)
        {
            notifyIcon_cd.BalloonTipTitle = title;
            notifyIcon_cd.BalloonTipText = text;
            notifyIcon_cd.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon_cd.Visible = true;
            notifyIcon_cd.ShowBalloonTip(5000);
        }

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

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 获取主显示器工作区域（排除任务栏）
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);

            // 计算右下角坐标
            int x = workingArea.Right - this.Width;
            int y = workingArea.Bottom - this.Height;

            // 设置窗体位置
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(x, y);

            // E2E 自测模式：CT_E2E=1 时自动回归"开始计时→切页签→计时存活"
            if (Environment.GetEnvironmentVariable("CT_E2E") == "1")
            {
                _ = RunE2eSelfTest();
            }
        }

        private async System.Threading.Tasks.Task RunE2eSelfTest()
        {
            string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ct_e2e_result.log");
            System.IO.File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] e2e begin\n");
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            notifyIcon_cd.Visible = false;
            base.OnFormClosed(e);
        }
    }
}
