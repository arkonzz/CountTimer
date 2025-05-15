using AntdUI;

namespace CountTimer.View
{
    public partial class AddEventForm : Window
    {
        public AddEventForm()
        {
            InitializeComponent();
        }

        private void btn_close_Click(object sender, EventArgs e)
        {
            this.Hide();
        }
    }
}
