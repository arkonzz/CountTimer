using System.Configuration;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;


namespace CountTimer.View
{
    public partial class AddEventForm : Window
    {
        private ToDoThingService service;
        private Window window;
        private ToDoThing toDoThing;
        public event Action DataUpdated;

        public AddEventForm()
        {
            InitializeComponent();
            service = new ToDoThingServiceImpl();
            //初始化消息弹出位置
            Config.ShowInWindow = true;
            window = this;
            dp_endtime.MinDate = DateTime.Now;
            dp_endtime.Value = DateTime.Now;
            toDoThing = new ToDoThing();

        }

        private void btn_close_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void btn_add_Click(object sender, EventArgs e)
        {
            if (input_info.Text.Trim().Length == 0 || select_regular.SelectedValue == null)
            {
                AntdUI.Message.warn(window, "请补充内容", autoClose: 2);
                return;
            }
            toDoThing.toDoInfo = input_info.Text;
            toDoThing.isMailed = false;
            toDoThing.endTime = dp_endtime.Text;
            toDoThing.isRegular = select_regular.SelectedIndex == 1 ? false : true;
            if (service.AddToDoThing(toDoThing) > 0)
            {
                AntdUI.Message.success(window, "新增成功", autoClose: 1);
                input_info.Text = "";
                DataUpdated?.Invoke(); // 触发事件
                this.Hide();
            }
            else
            {
                AntdUI.Message.error(window, "新增失败", autoClose: 2);
            }

        }

    }
}
