using System.Windows.Forms;

namespace CountTimer
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be used; false, and nothing should be disposed.</param>
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
        /// Required for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            pageHeader1 = new AntdUI.PageHeader();
            tabs = new AntdUI.Tabs();
            tabPomodoro = new AntdUI.TabPage();
            tabMemo = new AntdUI.TabPage();
            tabHoliday = new AntdUI.TabPage();
            notifyIcon_cd = new System.Windows.Forms.NotifyIcon(components);
            contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(components);
            showWindowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            SuspendLayout();
            contextMenuStrip1.SuspendLayout();
            //
            // pageHeader1
            //
            pageHeader1.Dock = System.Windows.Forms.DockStyle.Top;
            pageHeader1.Location = new System.Drawing.Point(0, 0);
            pageHeader1.Name = "pageHeader1";
            pageHeader1.ShowButton = true;
            pageHeader1.Size = new System.Drawing.Size(660, 40);
            pageHeader1.TabIndex = 0;
            pageHeader1.Text = "番茄倒计时";
            //
            // tabs
            //
            tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            tabs.Location = new System.Drawing.Point(0, 40);
            tabs.Name = "tabs";
            tabs.Size = new System.Drawing.Size(660, 620);
            tabs.TabIndex = 1;
            tabs.Pages.Add(tabPomodoro);
            tabs.Pages.Add(tabMemo);
            tabs.Pages.Add(tabHoliday);
            //
            // tabPomodoro
            //
            tabPomodoro.Location = new System.Drawing.Point(0, 40);
            tabPomodoro.Name = "tabPomodoro";
            tabPomodoro.Padding = new System.Windows.Forms.Padding(3);
            tabPomodoro.Size = new System.Drawing.Size(656, 576);
            tabPomodoro.TabIndex = 0;
            tabPomodoro.Text = "番茄钟";
            tabPomodoro.IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"#888888\" fill-rule=\"evenodd\"><path d=\"M12 6 A7.5 7.5 0 1 0 12 21 A7.5 7.5 0 1 0 12 6 Z M12 9 A4.5 4.5 0 1 1 12 18 A4.5 4.5 0 1 1 12 9 Z M11.4 4 L12.6 4 L12.6 12 L11.4 12 Z\"/></svg>";
            //
            // tabMemo
            //
            tabMemo.Location = new System.Drawing.Point(0, 40);
            tabMemo.Name = "tabMemo";
            tabMemo.Padding = new System.Windows.Forms.Padding(3);
            tabMemo.Size = new System.Drawing.Size(676, 640);
            tabMemo.TabIndex = 1;
            tabMemo.Text = "小计";
            tabMemo.IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"#888888\"><rect x=\"3\" y=\"4.7\" width=\"2.6\" height=\"2.6\" rx=\"1.3\"/><rect x=\"8\" y=\"4.7\" width=\"13\" height=\"2.6\" rx=\"1.3\"/><rect x=\"3\" y=\"10.7\" width=\"2.6\" height=\"2.6\" rx=\"1.3\"/><rect x=\"8\" y=\"10.7\" width=\"13\" height=\"2.6\" rx=\"1.3\"/><rect x=\"3\" y=\"16.7\" width=\"2.6\" height=\"2.6\" rx=\"1.3\"/><rect x=\"8\" y=\"16.7\" width=\"13\" height=\"2.6\" rx=\"1.3\"/></svg>";
            //
            // tabHoliday
            //
            tabHoliday.Location = new System.Drawing.Point(0, 40);
            tabHoliday.Name = "tabHoliday";
            tabHoliday.Padding = new System.Windows.Forms.Padding(3);
            tabHoliday.Size = new System.Drawing.Size(656, 576);
            tabHoliday.TabIndex = 2;
            tabHoliday.Text = "节假日";
            tabHoliday.IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"#888888\" fill-rule=\"evenodd\"><path d=\"M5 4 L19 4 A2 2 0 0 1 21 6 L21 20 A2 2 0 0 1 19 22 L5 22 A2 2 0 0 1 3 20 L3 6 A2 2 0 0 1 5 4 Z M5 6 L5 20 L19 20 L19 6 Z\"/><rect x=\"7\" y=\"2\" width=\"2\" height=\"4\" rx=\"1\"/><rect x=\"15\" y=\"2\" width=\"2\" height=\"4\" rx=\"1\"/><rect x=\"3\" y=\"9\" width=\"18\" height=\"2\"/></svg>";
            //
            // notifyIcon_cd
            //
            notifyIcon_cd.ContextMenuStrip = contextMenuStrip1;
            notifyIcon_cd.Icon = ((System.Drawing.Icon)resources.GetObject("notifyIcon_cd.Icon"));
            notifyIcon_cd.Text = "番茄倒计时";
            notifyIcon_cd.Visible = true;
            notifyIcon_cd.MouseClick += notifyIcon_Click;
            //
            // contextMenuStrip1
            //
            contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { showWindowToolStripMenuItem, exitToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new System.Drawing.Size(137, 48);
            //
            // showWindowToolStripMenuItem
            //
            showWindowToolStripMenuItem.Name = "showWindowToolStripMenuItem";
            showWindowToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            showWindowToolStripMenuItem.Text = "显示主界面";
            showWindowToolStripMenuItem.Click += showWindowMenuItem_Click;
            //
            // exitToolStripMenuItem
            //
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            exitToolStripMenuItem.Text = "退出";
            exitToolStripMenuItem.Click += exitMenuItem_Click;
            //
            // MainForm
            //
            ClientSize = new System.Drawing.Size(680, 720);
            MinimumSize = new System.Drawing.Size(620, 680);
            Controls.Add(tabs);
            Controls.Add(pageHeader1);
            Icon = ((System.Drawing.Icon)resources.GetObject("$this.Icon"));
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "番茄倒计时";
            FormClosing += MainForm_FormClosing;
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.PageHeader pageHeader1;
        private AntdUI.Tabs tabs;
        private AntdUI.TabPage tabPomodoro;
        private AntdUI.TabPage tabMemo;
        private AntdUI.TabPage tabHoliday;
        private NotifyIcon notifyIcon_cd;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem showWindowToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
    }
}
