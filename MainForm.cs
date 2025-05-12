using AntdUI;
using Timer = System.Windows.Forms.Timer;

namespace CountTimer
{
    public partial class MainForm :Window
    {
        private DateTime _targetTime;
        private readonly Timer _timer = new Timer();
        public MainForm()
        {
            InitializeComponent();
            _timer.Tick += Timer_Tick; // 确保事件绑定
        }

        private void btn_countdown_Click(object sender, EventArgs e)
        {
            _targetTime = (DateTime)countdown_timer.Value;
            if (_targetTime <= DateTime.Now)
            {
                MessageBox.Show("请选择未来的时间！");
                return;
            }


            _timer.Start();
            //UpdateCountdown();
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
    }
}
