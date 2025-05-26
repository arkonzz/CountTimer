namespace CountTimer.View
{
    partial class AddEventForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panel1 = new AntdUI.Panel();
            label2 = new AntdUI.Label();
            label1 = new AntdUI.Label();
            select_regular = new AntdUI.Select();
            dp_endtime = new AntdUI.DatePicker();
            btn_close = new AntdUI.Button();
            btn_add = new AntdUI.Button();
            panel2 = new AntdUI.Panel();
            input_info = new AntdUI.Input();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(label2);
            panel1.Controls.Add(label1);
            panel1.Controls.Add(select_regular);
            panel1.Controls.Add(dp_endtime);
            panel1.Controls.Add(btn_close);
            panel1.Controls.Add(btn_add);
            panel1.Dock = DockStyle.Top;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(405, 102);
            panel1.TabIndex = 0;
            panel1.Text = "panel1";
            // 
            // label2
            // 
            label2.BackColor = SystemColors.Window;
            label2.Location = new Point(3, 58);
            label2.Name = "label2";
            label2.Size = new Size(68, 23);
            label2.TabIndex = 5;
            label2.Text = "常规事件：";
            // 
            // label1
            // 
            label1.BackColor = SystemColors.Window;
            label1.Location = new Point(3, 12);
            label1.Name = "label1";
            label1.Size = new Size(68, 23);
            label1.TabIndex = 4;
            label1.Text = "事件时间：";
            // 
            // select_regular
            // 
            select_regular.Items.AddRange(new object[] { "是", "否" });
            select_regular.List = true;
            select_regular.Location = new Point(75, 49);
            select_regular.Margin = new Padding(2, 3, 2, 3);
            select_regular.Name = "select_regular";
            select_regular.PlaceholderText = "是否为常规事件";
            select_regular.Size = new Size(166, 39);
            select_regular.TabIndex = 3;
            // 
            // dp_endtime
            // 
            dp_endtime.Format = "yyyy-MM-dd HH:mm:ss";
            dp_endtime.Location = new Point(75, 3);
            dp_endtime.Name = "dp_endtime";
            dp_endtime.PlaceholderText = "请选择时间";
            dp_endtime.Size = new Size(207, 40);
            dp_endtime.TabIndex = 2;
            // 
            // btn_close
            // 
            btn_close.BorderWidth = 2F;
            btn_close.Location = new Point(327, 49);
            btn_close.Name = "btn_close";
            btn_close.Size = new Size(75, 40);
            btn_close.TabIndex = 1;
            btn_close.Text = "取消";
            btn_close.Click += btn_close_Click;
            // 
            // btn_add
            // 
            btn_add.Location = new Point(327, 3);
            btn_add.Name = "btn_add";
            btn_add.Size = new Size(75, 40);
            btn_add.TabIndex = 0;
            btn_add.Text = "新增";
            btn_add.Type = AntdUI.TTypeMini.Primary;
            btn_add.Click += btn_add_Click;
            // 
            // panel2
            // 
            panel2.Controls.Add(input_info);
            panel2.Dock = DockStyle.Bottom;
            panel2.Location = new Point(0, 114);
            panel2.Name = "panel2";
            panel2.Size = new Size(405, 110);
            panel2.TabIndex = 1;
            panel2.Text = "panel2";
            // 
            // input_info
            // 
            input_info.Dock = DockStyle.Fill;
            input_info.Location = new Point(0, 0);
            input_info.Name = "input_info";
            input_info.PlaceholderText = "请输入内容";
            input_info.Size = new Size(405, 110);
            input_info.TabIndex = 0;
            // 
            // AddEventForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(405, 224);
            Controls.Add(panel2);
            Controls.Add(panel1);
            Name = "AddEventForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "新增事件";
            VisibleChanged += AddEventForm_VisibleChanged;
            panel1.ResumeLayout(false);
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.Panel panel1;
        private AntdUI.Button btn_close;
        private AntdUI.Button btn_add;
        private AntdUI.DatePicker dp_endtime;
        private AntdUI.Panel panel2;
        private AntdUI.Input input_info;
        private AntdUI.Select select_regular;
        private AntdUI.Label label1;
        private AntdUI.Label label2;
    }
}