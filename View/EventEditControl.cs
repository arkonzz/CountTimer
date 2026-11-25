using System;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 事件新增/编辑控件（嵌入主窗体的 AntdUI 抽屉中，替代独立窗口）
    /// </summary>
    public class EventEditControl : UserControl, AntdUI.IEventListener
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private readonly ToDoThingService service = new ToDoThingServiceImpl();
        private bool _isEditMode;
        private ToDoThing? _editThing;

        private readonly AntdUI.Label lblTitle = new AntdUI.Label();
        private readonly AntdUI.Label lblContent = new AntdUI.Label();
        private readonly AntdUI.Label lblTime = new AntdUI.Label();
        private readonly AntdUI.Label lblRegular = new AntdUI.Label();
        private readonly AntdUI.Input input_info = new AntdUI.Input();
        private readonly AntdUI.DatePicker dp_endtime = new AntdUI.DatePicker();
        private readonly AntdUI.Select select_regular = new AntdUI.Select();
        private readonly AntdUI.Button btn_close = new AntdUI.Button();
        private readonly AntdUI.Button btn_save = new AntdUI.Button();

        public event Action? DataUpdated;

        public EventEditControl()
        {
            Size = new Size(408, 264);
            BackColor = UiTheme.PageBg;
            BuildUi();
            this.AddListener();
            ApplyLanguage();
        }

        private void BuildUi()
        {
            lblTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            lblTitle.ForeColor = UiTheme.TextMain;
            lblTitle.Location = new Point(16, 12);
            lblTitle.Size = new Size(200, 28);
            Controls.Add(lblTitle);

            lblContent.Location = new Point(16, 56);
            lblContent.Size = new Size(72, 30);
            lblContent.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblContent);

            input_info.Location = new Point(96, 52);
            input_info.Size = new Size(296, 40);
            Controls.Add(input_info);

            lblTime.Location = new Point(16, 108);
            lblTime.Size = new Size(72, 30);
            lblTime.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblTime);

            dp_endtime.Format = DateTimeFormat;
            dp_endtime.Location = new Point(96, 104);
            dp_endtime.Size = new Size(296, 40);
            dp_endtime.Value = DateTime.Now;
            Controls.Add(dp_endtime);

            lblRegular.Location = new Point(16, 160);
            lblRegular.Size = new Size(72, 30);
            lblRegular.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblRegular);

            select_regular.List = true;
            select_regular.Location = new Point(96, 156);
            select_regular.Size = new Size(296, 40);
            Controls.Add(select_regular);

            btn_close.Ghost = true;
            btn_close.BorderWidth = 1F;
            btn_close.Location = new Point(204, 212);
            btn_close.Size = new Size(88, 40);
            btn_close.Click += btn_close_Click;
            Controls.Add(btn_close);

            btn_save.Type = AntdUI.TTypeMini.Primary;
            btn_save.Location = new Point(304, 212);
            btn_save.Size = new Size(88, 40);
            btn_save.Click += btn_save_Click;
            Controls.Add(btn_save);
        }

        /// <summary>语言切换/初始化：重刷静态文案（下拉项重建并保持选中态）</summary>
        public void ApplyLanguage()
        {
            lblTitle.Text = _isEditMode ? I18n.T("编辑事件", "Edit event") : I18n.T("新增事件", "Add event");
            lblContent.Text = I18n.T("事件内容", "Content");
            input_info.PlaceholderText = I18n.T("请输入内容", "Enter content");
            lblTime.Text = I18n.T("事件时间", "Time");
            dp_endtime.PlaceholderText = I18n.T("请选择时间", "Pick a time");
            lblRegular.Text = I18n.T("重复", "Repeats");

            int repeatIndex = select_regular.SelectedIndex;
            select_regular.Items.Clear();
            select_regular.Items.AddRange(RepeatOptionTexts());
            select_regular.SelectedIndex = repeatIndex;

            btn_close.Text = I18n.T("取消", "Cancel");
            btn_save.Text = _isEditMode ? I18n.T("保存", "Save") : I18n.T("新增", "Add");
        }

        /// <summary>下拉序号即 RepeatKind 的数值：不重复 / 每天 / 工作日 / 每周</summary>
        private static object[] RepeatOptionTexts() =>
        [
            I18n.T("不重复", "Never"),
            I18n.T("每天", "Daily"),
            I18n.T("工作日", "Weekdays"),
            I18n.T("每周（同所选时间的星期）", "Weekly (same weekday)"),
        ];

        public void PrepareAddEvent()
        {
            _isEditMode = false;
            _editThing = null;
            lblTitle.Text = I18n.T("新增事件", "Add event");
            input_info.Text = "";
            dp_endtime.MinDate = DateTime.Now;
            dp_endtime.Value = DateTime.Now;
            select_regular.SelectedIndex = 0;
            btn_save.Text = I18n.T("新增", "Add");
        }

        public void SetEditEvent(ToDoThing thing)
        {
            _isEditMode = true;
            _editThing = thing;
            lblTitle.Text = I18n.T("编辑事件", "Edit event");
            input_info.Text = thing.toDoInfo;
            dp_endtime.MinDate = DateTime.MinValue;
            if (DateTime.TryParse(thing.endTime, out DateTime endTime))
            {
                dp_endtime.Value = endTime;
            }
            else
            {
                dp_endtime.Value = DateTime.Now;
                AntdUI.Message.warn(FindForm()!, I18n.T("事件时间格式异常，请重新选择", "Invalid event time — pick again"), autoClose: 2);
            }

            select_regular.SelectedIndex = (int)thing.Kind;
            btn_save.Text = I18n.T("保存", "Save");
        }

        private void btn_close_Click(object? sender, EventArgs e)
        {
            CloseDrawer();
        }

        private void btn_save_Click(object? sender, EventArgs e)
        {
            string toDoInfo = input_info.Text.Trim();
            if (toDoInfo.Length == 0)
            {
                AntdUI.Message.warn(FindForm()!, I18n.T("请输入内容", "Please enter the content"), autoClose: 2);
                return;
            }

            if (!DateTime.TryParse(dp_endtime.Text, out DateTime endTime))
            {
                AntdUI.Message.warn(FindForm()!, I18n.T("请选择有效时间", "Please pick a valid time"), autoClose: 2);
                return;
            }

            // 下拉序号即 RepeatKind 的数值
            var kind = (RepeatKind)Math.Clamp(select_regular.SelectedIndex, 0, (int)RepeatKind.Weekly);
            if (kind == RepeatKind.None && endTime <= DateTime.Now)
            {
                AntdUI.Message.warn(FindForm()!, I18n.T("事件时间必须晚于当前时间", "Time must be later than now"), autoClose: 2);
                return;
            }

            // 重复事件从所选时间起算下一次：每周取所选日期的星期，时间取所选时刻
            if (kind != RepeatKind.None)
            {
                endTime = RepeatRule.Next(kind, (int)endTime.DayOfWeek, endTime.TimeOfDay, DateTime.Now);
            }

            if (_isEditMode)
            {
                if (_editThing == null)
                {
                    AntdUI.Message.error(FindForm()!, I18n.T("编辑数据异常", "Unexpected edit state"), autoClose: 2);
                    PrepareAddEvent();
                    return;
                }

                ApplyFields(_editThing, toDoInfo, kind, endTime);
                if (service.UpdateToDoThing(_editThing) > 0)
                {
                    AntdUI.Message.success(FindForm()!, I18n.T("修改成功", "Saved"), autoClose: 1);
                    DataUpdated?.Invoke();
                    CloseDrawer();
                }
                else
                {
                    AntdUI.Message.error(FindForm()!, I18n.T("修改失败", "Save failed"), autoClose: 2);
                }
                return;
            }

            var toDoThing = new ToDoThing { isMailed = false };
            ApplyFields(toDoThing, toDoInfo, kind, endTime);

            if (service.AddToDoThing(toDoThing) > 0)
            {
                AntdUI.Message.success(FindForm()!, I18n.T("新增成功", "Added"), autoClose: 1);
                DataUpdated?.Invoke();
                CloseDrawer();
            }
            else
            {
                AntdUI.Message.error(FindForm()!, I18n.T("新增失败", "Add failed"), autoClose: 2);
            }
        }

        /// <summary>isRegular 是"是否重复"的旧字段，保留给原有查询用；具体方式写在 repeatKind</summary>
        private static void ApplyFields(ToDoThing thing, string toDoInfo, RepeatKind kind, DateTime endTime)
        {
            thing.toDoInfo = toDoInfo;
            thing.endTime = endTime.ToString(DateTimeFormat);
            thing.isRegular = kind != RepeatKind.None;
            thing.repeatKind = (int)kind;
            thing.repeatDay = (int)endTime.DayOfWeek;
        }

        /// <summary>
        /// 关闭抽屉：内容控件的宿主只是内部容器，需要关闭 AntdUI 的抽屉层窗体
        /// （LayeredFormDrawer 关闭时会带走遮罩层）
        /// </summary>
        internal void CloseDrawer()
        {
            DrawerHelper.CloseOpenDrawers();
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                BackColor = UiTheme.PageBg;
                lblTitle.ForeColor = UiTheme.TextMain;
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();
    }
}
