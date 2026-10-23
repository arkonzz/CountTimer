using System;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;

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
        }

        private void BuildUi()
        {
            lblTitle.Text = "新增事件";
            lblTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            lblTitle.ForeColor = UiTheme.TextMain;
            lblTitle.Location = new Point(16, 12);
            lblTitle.Size = new Size(200, 28);
            Controls.Add(lblTitle);

            lblContent.Text = "事件内容";
            lblContent.Location = new Point(16, 56);
            lblContent.Size = new Size(72, 30);
            lblContent.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblContent);

            input_info.PlaceholderText = "请输入内容";
            input_info.Location = new Point(96, 52);
            input_info.Size = new Size(296, 40);
            Controls.Add(input_info);

            lblTime.Text = "事件时间";
            lblTime.Location = new Point(16, 108);
            lblTime.Size = new Size(72, 30);
            lblTime.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblTime);

            dp_endtime.Format = DateTimeFormat;
            dp_endtime.PlaceholderText = "请选择时间";
            dp_endtime.Location = new Point(96, 104);
            dp_endtime.Size = new Size(296, 40);
            dp_endtime.Value = DateTime.Now;
            Controls.Add(dp_endtime);

            lblRegular.Text = "每日循环";
            lblRegular.Location = new Point(16, 160);
            lblRegular.Size = new Size(72, 30);
            lblRegular.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblRegular);

            select_regular.Items.AddRange(new object[] { "是", "否" });
            select_regular.List = true;
            select_regular.PlaceholderText = "是否每日循环";
            select_regular.Location = new Point(96, 156);
            select_regular.Size = new Size(296, 40);
            Controls.Add(select_regular);

            btn_close.Text = "取消";
            btn_close.Ghost = true;
            btn_close.BorderWidth = 1F;
            btn_close.Location = new Point(204, 212);
            btn_close.Size = new Size(88, 40);
            btn_close.Click += btn_close_Click;
            Controls.Add(btn_close);

            btn_save.Text = "新增";
            btn_save.Type = AntdUI.TTypeMini.Primary;
            btn_save.Location = new Point(304, 212);
            btn_save.Size = new Size(88, 40);
            btn_save.Click += btn_save_Click;
            Controls.Add(btn_save);
        }

        public void PrepareAddEvent()
        {
            _isEditMode = false;
            _editThing = null;
            lblTitle.Text = "新增事件";
            input_info.Text = "";
            dp_endtime.MinDate = DateTime.Now;
            dp_endtime.Value = DateTime.Now;
            select_regular.SelectedIndex = -1;
            btn_save.Text = "新增";
        }

        public void SetEditEvent(ToDoThing thing)
        {
            _isEditMode = true;
            _editThing = thing;
            lblTitle.Text = "编辑事件";
            input_info.Text = thing.toDoInfo;
            dp_endtime.MinDate = DateTime.MinValue;
            if (DateTime.TryParse(thing.endTime, out DateTime endTime))
            {
                dp_endtime.Value = endTime;
            }
            else
            {
                dp_endtime.Value = DateTime.Now;
                AntdUI.Message.warn(FindForm()!, "事件时间格式异常，请重新选择", autoClose: 2);
            }

            select_regular.SelectedIndex = thing.isRegular ? 0 : 1;
            btn_save.Text = "保存";
        }

        private void btn_close_Click(object? sender, EventArgs e)
        {
            CloseDrawer();
        }

        private void btn_save_Click(object? sender, EventArgs e)
        {
            string toDoInfo = input_info.Text.Trim();
            if (toDoInfo.Length == 0 || select_regular.SelectedIndex < 0)
            {
                AntdUI.Message.warn(FindForm()!, "请补充内容", autoClose: 2);
                return;
            }

            if (!DateTime.TryParse(dp_endtime.Text, out DateTime endTime))
            {
                AntdUI.Message.warn(FindForm()!, "请选择有效时间", autoClose: 2);
                return;
            }

            bool isRegular = select_regular.SelectedIndex == 0;
            if (!isRegular && endTime <= DateTime.Now)
            {
                AntdUI.Message.warn(FindForm()!, "普通事件时间必须晚于当前时间", autoClose: 2);
                return;
            }

            endTime = NormalizeEndTime(endTime, isRegular);
            string endTimeText = endTime.ToString(DateTimeFormat);

            if (_isEditMode)
            {
                if (_editThing == null)
                {
                    AntdUI.Message.error(FindForm()!, "编辑数据异常", autoClose: 2);
                    PrepareAddEvent();
                    return;
                }

                _editThing.toDoInfo = toDoInfo;
                _editThing.endTime = endTimeText;
                _editThing.isRegular = isRegular;
                if (service.UpdateToDoThing(_editThing) > 0)
                {
                    AntdUI.Message.success(FindForm()!, "修改成功", autoClose: 1);
                    DataUpdated?.Invoke();
                    CloseDrawer();
                }
                else
                {
                    AntdUI.Message.error(FindForm()!, "修改失败", autoClose: 2);
                }
                return;
            }

            var toDoThing = new ToDoThing
            {
                toDoInfo = toDoInfo,
                isMailed = false,
                endTime = endTimeText,
                isRegular = isRegular
            };

            if (service.AddToDoThing(toDoThing) > 0)
            {
                AntdUI.Message.success(FindForm()!, "新增成功", autoClose: 1);
                DataUpdated?.Invoke();
                CloseDrawer();
            }
            else
            {
                AntdUI.Message.error(FindForm()!, "新增失败", autoClose: 2);
            }
        }

        /// <summary>
        /// 关闭抽屉：内容控件的宿主只是内部容器，需要关闭 AntdUI 的抽屉层窗体
        /// （LayeredFormDrawer 关闭时会带走遮罩层）
        /// </summary>
        internal void CloseDrawer()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if (form.GetType().Name.Contains("LayeredFormDrawer"))
                {
                    form.Close();
                }
            }
        }

        private static DateTime NormalizeEndTime(DateTime endTime, bool isRegular)
        {
            if (!isRegular || endTime > DateTime.Now)
            {
                return endTime;
            }

            DateTime nextOccurrence = DateTime.Today.Add(endTime.TimeOfDay);
            return nextOccurrence > DateTime.Now ? nextOccurrence : nextOccurrence.AddDays(1);
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
