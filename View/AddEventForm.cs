using System;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;


namespace CountTimer.View
{
    public partial class AddEventForm : Window
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private readonly ToDoThingService service;
        private readonly Window window;
        private bool _isEditMode;
        private ToDoThing? _editThing;
        public event Action? DataUpdated;

        public AddEventForm()
        {
            InitializeComponent();
            service = new ToDoThingServiceImpl();
            //初始化消息弹出位置
            Config.ShowInWindow = true;
            window = this;
            dp_endtime.MinDate = DateTime.Now;
            dp_endtime.Value = DateTime.Now;

        }

        public void PrepareAddEvent()
        {
            ResetForm();
        }

        public void SetEditEvent(ToDoThing thing)
        {
            _isEditMode = true;
            _editThing = thing;
            input_info.Text = thing.toDoInfo;
            dp_endtime.MinDate = DateTime.MinValue;
            if (DateTime.TryParse(thing.endTime, out DateTime endTime))
            {
                dp_endtime.Value = endTime;
            }
            else
            {
                dp_endtime.Value = DateTime.Now;
                AntdUI.Message.warn(window, "事件时间格式异常，请重新选择", autoClose: 2);
            }
            select_regular.SelectedIndex = thing.isRegular ? 0 : 1;
            btn_add.Text = "保存";
            this.Text = "编辑事件";
        }

        private void ResetForm()
        {
            _isEditMode = false;
            _editThing = null;
            input_info.Text = "";
            dp_endtime.MinDate = DateTime.Now;
            dp_endtime.Value = DateTime.Now;
            select_regular.SelectedIndex = -1;
            btn_add.Text = "新增";
            this.Text = "新增事件";
        }

        private void btn_close_Click(object sender, EventArgs e)
        {
            ResetForm();
            this.Hide();
        }

        private void btn_add_Click(object sender, EventArgs e)
        {
            string toDoInfo = input_info.Text.Trim();
            if (toDoInfo.Length == 0 || select_regular.SelectedIndex < 0)
            {
                AntdUI.Message.warn(window, "请补充内容", autoClose: 2);
                return;
            }

            if (!DateTime.TryParse(dp_endtime.Text, out DateTime endTime))
            {
                AntdUI.Message.warn(window, "请选择有效时间", autoClose: 2);
                return;
            }

            bool isRegular = select_regular.SelectedIndex == 0;
            if (!isRegular && endTime <= DateTime.Now)
            {
                AntdUI.Message.warn(window, "普通事件时间必须晚于当前时间", autoClose: 2);
                return;
            }

            endTime = NormalizeEndTime(endTime, isRegular);
            string endTimeText = endTime.ToString(DateTimeFormat);

            if (_isEditMode)
            {
                if (_editThing == null)
                {
                    AntdUI.Message.error(window, "编辑数据异常", autoClose: 2);
                    ResetForm();
                    return;
                }

                _editThing.toDoInfo = toDoInfo;
                _editThing.endTime = endTimeText;
                _editThing.isRegular = isRegular;
                if (service.UpdateToDoThing(_editThing) > 0)
                {
                    AntdUI.Message.success(window, "修改成功", autoClose: 1);
                    DataUpdated?.Invoke();
                    ResetForm();
                    this.Hide();
                }
                else
                {
                    AntdUI.Message.error(window, "修改失败", autoClose: 2);
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
                AntdUI.Message.success(window, "新增成功", autoClose: 1);
                DataUpdated?.Invoke(); // 触发事件
                ResetForm();
                this.Hide();
            }
            else
            {
                AntdUI.Message.error(window, "新增失败", autoClose: 2);
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

        private void AddEventForm_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible && !_isEditMode)
            {
                dp_endtime.MinDate = DateTime.Now;
                dp_endtime.Value = DateTime.Now;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                ResetForm();
                Hide();
                return;
            }

            base.OnFormClosing(e);
        }
    }
}
