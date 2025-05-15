using System.Configuration;
using AntdUI;
using CountTimer.View;
using Timer = System.Windows.Forms.Timer;

namespace CountTimer
{
    public partial class MainForm : Window
    {
        private DateTime _targetTime;
        private readonly Timer _timer = new Timer();
        private bool dispose = false;
        private DateTime lastTime;
        private Configuration config;
        private AddEventForm addEventForm;
        public MainForm()
        {
            InitializeComponent();
            _timer.Tick += Timer_Tick; // 确保事件绑定
            this.TopMost = true;
            lastTime = Convert.ToDateTime(ConfigurationManager.AppSettings["lastTime"]);
            // 获取配置文件
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        }

        private void btn_countdown_Click(object sender, EventArgs e)
        {
            _targetTime = lastTime;
            if (_targetTime <= DateTime.Now)
            {
                MessageBox.Show("请选择未来的时间！");
                return;
            }
            _timer.Start();
            // 修改属性值
            config.AppSettings.Settings["lastTime"].Value = lastTime.ToString();
            // 保存配置文件
            config.Save(ConfigurationSaveMode.Modified);
            // 刷新配置文件
            ConfigurationManager.RefreshSection("appSettings");
        }
        private void UpdateCountdown()
        {
            TimeSpan remaining = _targetTime - DateTime.Now;
            lblCountdown.Text = FormatTimeSpan(remaining);

            if (remaining.TotalSeconds <= 0)
            {
                _timer.Stop();
                lblCountdown.Text = "时间到！";
                MessageBox.Show("倒计时结束！");
            }
        }
        // 定时器事件
        private void Timer_Tick(object sender, EventArgs e) => UpdateCountdown();
        // 格式化时间为 "天:时:分:秒"
        private static string FormatTimeSpan(TimeSpan ts)
        {
            return ts.TotalSeconds > 0
                ? $"{ts.Days:D2} 天 {ts.Hours:D2} 时 {ts.Minutes:D2} 分 {ts.Seconds:D2} 秒"
                : "00 天 00 时 00 分 00 秒";
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

        // 在托盘图标右键点菜单“显示界面”时显示窗体
        private void showWindowMenuItem_Click(object sender, EventArgs e)
        {
            windowDisplay(true);
        }

        // 在托盘图标右键点菜单“退出”时退出程序
        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            dispose = true;
            Application.Exit();
        }

        // 点击托盘图标显示出窗体
        private void notifyIcon_Click(object sender, MouseEventArgs e)
        {
            // 需要将事件转换成鼠标事件
            MouseEventArgs mouseEvent = (MouseEventArgs)e;
            if (mouseEvent.Button == MouseButtons.Left) // 点击左键才弹出
            {
                windowDisplay(true);
            }
        }

        //
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (dispose)
            {
                Application.Exit();
            }
            else
            {
                windowDisplay(false);
                e.Cancel = true;
            }

        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 获取主显示器工作区域（排除任务栏）
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;

            // 计算右下角坐标
            int x = workingArea.Right - this.Width;
            int y = workingArea.Bottom - this.Height;

            // 设置窗体位置
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(x, y);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _timer.Stop();
        }

        private void btn_addEvent_Click(object sender, EventArgs e)
        {
            if (addEventForm == null)
            {
                addEventForm = new AddEventForm();
            }

            addEventForm.Show();
        }
    }
}
