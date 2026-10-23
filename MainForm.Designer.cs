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
            //
            // tabPomodoro
            //
            tabPomodoro.Location = new System.Drawing.Point(0, 40);
            tabPomodoro.Name = "tabPomodoro";
            tabPomodoro.Padding = new System.Windows.Forms.Padding(3);
            tabPomodoro.Size = new System.Drawing.Size(656, 576);
            tabPomodoro.TabIndex = 0;
            tabPomodoro.Text = "番茄钟";
            tabPomodoro.IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><line x1=\"10\" y1=\"2\" x2=\"14\" y2=\"2\"/><line x1=\"12\" y1=\"14\" x2=\"15\" y2=\"11\"/><circle cx=\"12\" cy=\"14\" r=\"8\"/></svg>";
            //
            // tabMemo
            //
            tabMemo.Location = new System.Drawing.Point(0, 40);
            tabMemo.Name = "tabMemo";
            tabMemo.Padding = new System.Windows.Forms.Padding(3);
            tabMemo.Size = new System.Drawing.Size(676, 640);
            tabMemo.TabIndex = 1;
            tabMemo.Text = "小计";
            tabMemo.IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><line x1=\"8\" y1=\"6\" x2=\"21\" y2=\"6\"/><line x1=\"8\" y1=\"12\" x2=\"21\" y2=\"12\"/><line x1=\"8\" y1=\"18\" x2=\"21\" y2=\"18\"/><line x1=\"3\" y1=\"6\" x2=\"3.01\" y2=\"6\"/><line x1=\"3\" y1=\"12\" x2=\"3.01\" y2=\"12\"/><line x1=\"3\" y1=\"18\" x2=\"3.01\" y2=\"18\"/></svg>";
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
        private NotifyIcon notifyIcon_cd;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem showWindowToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
    }
}
