using System.Configuration;
using System.Diagnostics;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;
using CountTimer.View;
using Timer = System.Windows.Forms.Timer;

namespace CountTimer
{
    public partial class MainForm : Window
    {
        private DateTime _targetTime;
        private readonly Timer _timer = new Timer();
        private bool dispose = false;
        private AddEventForm addEventForm;
        private ToDoThingService service;
        List<ToDoThing> toDoThings;
        private CancellationTokenSource _cts;
        private Window window;
        public MainForm()
        {
            InitializeComponent();
            _timer.Tick += Timer_Tick; // 确保事件绑定
            this.TopMost = true;
            window = this;
            service = new ToDoThingServiceImpl();
            _cts = new CancellationTokenSource();
            service.updateRegularEvent();
            deleteExpiredEvent();
        }

        private void deleteExpiredEvent()
        {
            Task.Run(async () => {
                while (!_cts.IsCancellationRequested)
                 {
                     service.deleteByTime(DateTime.Now);
                     select_event.Invoke(() => {
                       initSelectEvent();
                      });
                    await Task.Delay(30 * 1000); // 添加await关键字
                }
                
             });
        }

        private void btn_countdown_Click(object sender, EventArgs e)
        {
            if(select_event.SelectedValue == null)
            {
                AntdUI.Message.error(window, "请选择事件", autoClose: 2);
                return;
            }
            ToDoThing toDoThing = (ToDoThing)select_event.SelectedValue;
            _targetTime = Convert.ToDateTime(toDoThing.endTime);
            if (_targetTime <= DateTime.Now)
            {
                _timer.Stop();
                lblCountdown.Text = "事件已结束";
                //AntdUI.Message.warn(window, "事件结束", autoClose: 2);
                return;
            }
            _timer.Start();
        }
        private void UpdateCountdown()
        {
            TimeSpan remaining = _targetTime - DateTime.Now;
            lblCountdown.Text = FormatTimeSpan(remaining);

            if (remaining.TotalSeconds <= 0)
            {
                _timer.Stop();
                lblCountdown.Text = "时间到！";
                //MessageBox.Show("倒计时结束！");
                AntdUI.Message.warn(window, select_event.Text+"事件结束", autoClose: 2);
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
        private void initSelectEvent()
        {
            select_event.Items.Clear();
            toDoThings = service.GetTodoList();
           // var list = new List<SelectItem>();
            
            select_event.Items.AddRange([.. toDoThings]);
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
            addEventForm.DataUpdated += () =>
            {
                
                initSelectEvent();
            };
            addEventForm.Show();

        }

        private void select_event_SelectedValueChanged(object sender, ObjectNEventArgs e)
        {
            btn_countdown.PerformClick();
        }
    }
}
