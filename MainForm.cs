using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;
using CountTimer.View;

namespace CountTimer
{
    public partial class MainForm : Window
    {
        private readonly CountdownController _countdownController = new CountdownController();
        private readonly ExpiredEventCleanupWorker _cleanupWorker = new ExpiredEventCleanupWorker();
        private bool _isExitRequested = false;
        private bool _isRefreshingEvents = false;
        private AddEventForm? addEventForm;
        private readonly ToDoThingService service;
        private List<ToDoThing> toDoThings = [];
        private readonly Window window;
        public MainForm()
        {
            InitializeComponent();
            _countdownController.Tick += CountdownController_Tick;
            _countdownController.Completed += CountdownController_Completed;
            this.TopMost = true;
            window = this;
            service = new ToDoThingServiceImpl();
            service.updateRegularEvent();
            _cleanupWorker.Start(RefreshEventsFromWorker, NotifyRegularEventFromWorker);
        }

        private void btn_countdown_Click(object sender, EventArgs e)
        {
            if (select_event.SelectedValue is not ToDoThing toDoThing)
            {
                AntdUI.Message.error(window, "请选择事件", autoClose: 2);
                return;
            }

            if (!_countdownController.Start(toDoThing, out string? errorMessage))
            {
                lblCountdown.Text = errorMessage ?? "倒计时启动失败";
                if (errorMessage == "事件时间格式异常")
                {
                    AntdUI.Message.error(window, errorMessage, autoClose: 2);
                }
                return;
            }
        }

        private void CountdownController_Tick(TimeSpan remaining, ToDoThing toDoThing)
        {
            lblCountdown.Text = CountdownController.FormatTimeSpan(remaining, toDoThing.isRegular);
        }

        private void CountdownController_Completed(ToDoThing toDoThing)
        {
            lblCountdown.Text = "时间到！";
            AntdUI.Message.warn(window, $"{toDoThing.toDoInfo} 事件结束", autoClose: 2);
            ShowDesktopNotification(toDoThing.toDoInfo);
        }

        private void ShowDesktopNotification(string eventName)
        {
            notifyIcon_cd.BalloonTipTitle = "倒计时结束";
            notifyIcon_cd.BalloonTipText = string.IsNullOrWhiteSpace(eventName)
                ? "时间到！"
                : $"{eventName} 事件结束";
            notifyIcon_cd.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon_cd.Visible = true;
            notifyIcon_cd.ShowBalloonTip(5000);
        }

        private void RefreshEventsFromWorker()
        {
            if (IsDisposed || Disposing || !select_event.IsHandleCreated)
            {
                return;
            }

            try
            {
                select_event.BeginInvoke((Action)InitSelectEvent);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void NotifyRegularEventFromWorker(ToDoThing toDoThing)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke((Action)(() =>
                {
                    AntdUI.Message.warn(window, $"{toDoThing.toDoInfo} 事件结束", autoClose: 2);
                    ShowDesktopNotification(toDoThing.toDoInfo);
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void InitSelectEvent()
        {
            _isRefreshingEvents = true;
            try
            {
                int? selectedId = (select_event.SelectedValue as ToDoThing)?.Id;
                select_event.Items.Clear();
                toDoThings = service.GetTodoList();

                select_event.Items.AddRange([.. toDoThings]);

                if (toDoThings.Count == 0)
                {
                    btn_del.Enabled = false;
                    btn_edit.Enabled = false;
                    _countdownController.Stop();
                    lblCountdown.Text = "暂无事件";
                    return;
                }

                if (selectedId.HasValue)
                {
                    int selectedIndex = toDoThings.FindIndex(it => it.Id == selectedId.Value);
                    if (selectedIndex >= 0)
                    {
                        select_event.SelectedIndex = selectedIndex;
                        return;
                    }

                    btn_del.Enabled = false;
                    btn_edit.Enabled = false;
                    _countdownController.Stop();
                    lblCountdown.Text = "事件已结束";
                }
            }
            finally
            {
                _isRefreshingEvents = false;
            }
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
            _isExitRequested = true;
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
            InitSelectEvent();

            // 获取主显示器工作区域（排除任务栏）
            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);

            // 计算右下角坐标
            int x = workingArea.Right - this.Width;
            int y = workingArea.Bottom - this.Height;

            // 设置窗体位置
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(x, y);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _countdownController.Stop();
        }

        private void btn_addEvent_Click(object sender, EventArgs e)
        {
            if (addEventForm == null || addEventForm.IsDisposed)
            {
                addEventForm = new AddEventForm();
            }
            
            // 先取消订阅，避免重复订阅
            addEventForm.DataUpdated -= OnDataUpdated;
            addEventForm.DataUpdated += OnDataUpdated;
            addEventForm.PrepareAddEvent();
            addEventForm.Show();
        }
        
        private void OnDataUpdated()
        {
            InitSelectEvent();
            _cleanupWorker.Wake();
        }


        private void select_event_SelectedValueChanged(object sender, ObjectNEventArgs e)
        {
            if (_isRefreshingEvents)
            {
                return;
            }

            if (select_event.SelectedValue is not ToDoThing)
            {
                btn_del.Enabled = false;
                btn_edit.Enabled = false;
                _countdownController.Stop();
                return;
            }

            btn_del.Enabled = true;
            btn_edit.Enabled = true;
            btn_countdown.PerformClick();
        }

        private void btn_del_Click(object sender, EventArgs e)
        {
            if (select_event.SelectedValue == null)
            {
                AntdUI.Message.warn(window, "请先选择要删除的事件", autoClose: 2);
                return;
            }
            
            ToDoThing selectedTodo = (ToDoThing)select_event.SelectedValue;
            
            // 使用 Modal.Config 配置对话框
            var config = new AntdUI.Modal.Config(window, "确认删除", $"确定要删除事件 \"{selectedTodo.toDoInfo}\" 吗？")
            {
                OnOk = (cfg) => {
                    service.deleteById(selectedTodo.Id);
                    AntdUI.Message.success(window, "删除成功", autoClose: 2);
                    InitSelectEvent();
                    
                    // 如果正在倒计时该事件，停止计时
                    _countdownController.Stop();
                    lblCountdown.Text = "事件已删除";
                    return true; // 返回 true 关闭对话框
                }
            };
            AntdUI.Modal.open(config);
        }

        private void btn_edit_Click(object sender, EventArgs e)
        {
            if (select_event.SelectedValue == null)
            {
                AntdUI.Message.warn(window, "请先选择要编辑的事件", autoClose: 2);
                return;
            }

            ToDoThing selectedTodo = (ToDoThing)select_event.SelectedValue;
            if (addEventForm == null || addEventForm.IsDisposed)
            {
                addEventForm = new AddEventForm();
            }

            addEventForm.DataUpdated -= OnDataUpdated;
            addEventForm.DataUpdated += OnDataUpdated;
            addEventForm.SetEditEvent(selectedTodo);
            addEventForm.Show();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _cleanupWorker.Dispose();
            _countdownController.Dispose();
            notifyIcon_cd.Visible = false;
            base.OnFormClosed(e);
        }
    }
}
