namespace CountTimer
{
    partial class MainForm
    {
        /// <summary>
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            panel1 = new AntdUI.Panel();
            pageHeader1 = new AntdUI.PageHeader();
            button1 = new AntdUI.Button();
            btn_countdown = new AntdUI.Button();
            countdown_timer = new AntdUI.DatePicker();
            lblCountdown = new AntdUI.Label();
            timer1 = new System.Windows.Forms.Timer(components);
            notifyIcon_cd = new NotifyIcon(components);
            contextMenuStrip1 = new ContextMenuStrip(components);
            退出ToolStripMenuItem = new ToolStripMenuItem();
            panel1.SuspendLayout();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(pageHeader1);
            panel1.Controls.Add(button1);
            panel1.Controls.Add(btn_countdown);
            panel1.Controls.Add(countdown_timer);
            panel1.Dock = DockStyle.Top;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(421, 87);
            panel1.TabIndex = 0;
            panel1.Text = "panel1";
            // 
            // pageHeader1
            // 
            pageHeader1.Dock = DockStyle.Top;
            pageHeader1.Location = new Point(0, 0);
            pageHeader1.Name = "pageHeader1";
            pageHeader1.ShowButton = true;
            pageHeader1.Size = new Size(421, 23);
            pageHeader1.TabIndex = 4;
            pageHeader1.Text = "倒计时";
            // 
            // button1
            // 
            button1.BorderWidth = 2F;
            button1.Location = new Point(305, 32);
            button1.Name = "button1";
            button1.Size = new Size(97, 40);
            button1.TabIndex = 3;
            button1.Text = "倒计时暂停";
            // 
            // btn_countdown
            // 
            btn_countdown.Location = new Point(202, 32);
            btn_countdown.Name = "btn_countdown";
            btn_countdown.Size = new Size(97, 40);
            btn_countdown.TabIndex = 2;
            btn_countdown.Text = "倒计时开始";
            btn_countdown.Type = AntdUI.TTypeMini.Primary;
            btn_countdown.Click += btn_countdown_Click;
            // 
            // countdown_timer
            // 
            countdown_timer.Format = "yyyy-MM-dd HH:mm:ss";
            countdown_timer.Location = new Point(0, 32);
            countdown_timer.Name = "countdown_timer";
            countdown_timer.Size = new Size(196, 44);
            countdown_timer.TabIndex = 1;
            countdown_timer.Text = "2025-06-30 17:30:00";
            countdown_timer.Value = new DateTime(2025, 6, 30, 17, 30, 0, 0);
            // 
            // lblCountdown
            // 
            lblCountdown.Dock = DockStyle.Fill;
            lblCountdown.Font = new Font("Microsoft YaHei UI", 15F);
            lblCountdown.Location = new Point(0, 87);
            lblCountdown.Name = "lblCountdown";
            lblCountdown.Size = new Size(421, 147);
            lblCountdown.TabIndex = 1;
            lblCountdown.Text = "显示时间";
            lblCountdown.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // timer1
            // 
            timer1.Enabled = true;
            timer1.Interval = 1000;
            // 
            // notifyIcon_cd
            // 
            notifyIcon_cd.ContextMenuStrip = contextMenuStrip1;
            notifyIcon_cd.Icon = (Icon)resources.GetObject("notifyIcon_cd.Icon");
            notifyIcon_cd.Text = "notifyIcon_cd";
            notifyIcon_cd.Visible = true;
            notifyIcon_cd.MouseDoubleClick += notifyIcon_Click;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { 退出ToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(101, 26);
            // 
            // 退出ToolStripMenuItem
            // 
            退出ToolStripMenuItem.Name = "退出ToolStripMenuItem";
            退出ToolStripMenuItem.Size = new Size(100, 22);
            退出ToolStripMenuItem.Text = "退出";
            退出ToolStripMenuItem.Click += exitMenuItem_Click;
            // 
            // MainForm
            // 
            ClientSize = new Size(421, 234);
            Controls.Add(lblCountdown);
            Controls.Add(panel1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MainForm";
            FormClosing += MainForm_FormClosing;
            panel1.ResumeLayout(false);
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.Panel panel1;
        private AntdUI.DatePicker countdown_timer;
        private AntdUI.Button btn_countdown;
        private AntdUI.Label lblCountdown;
        private AntdUI.Button button1;
        private AntdUI.PageHeader pageHeader1;
        private System.Windows.Forms.Timer timer1;
        private NotifyIcon notifyIcon_cd;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem 退出ToolStripMenuItem;
    }
}