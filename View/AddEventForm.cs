using System;
using System.Configuration;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;


namespace CountTimer.View
{
    public partial class AddEventForm : Window
    {
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
            dp_endtime.Value = DateTime.Parse(thing.endTime);
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
            if (input_info.Text.Trim().Length == 0 || select_regular.SelectedValue == null)
            {
                AntdUI.Message.warn(window, "请补充内容", autoClose: 2);
                return;
            }

            if (_isEditMode)
            {
                if (_editThing == null)
                {
                    AntdUI.Message.error(window, "编辑数据异常", autoClose: 2);
                    ResetForm();
                    return;
                }

                _editThing.toDoInfo = input_info.Text;
                _editThing.endTime = dp_endtime.Text;
                _editThing.isRegular = select_regular.SelectedIndex != 1;
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
                toDoInfo = input_info.Text,
                isMailed = false,
                endTime = dp_endtime.Text,
                isRegular = select_regular.SelectedIndex != 1
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
