using System;
using System.Collections.Generic;
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
    /// 小计页签：手动记录事件流水，可选时间（默认当前时间）
    /// </summary>
    public class MemoControl : UserControl, AntdUI.IEventListener
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private readonly MemoRecordService service = new MemoRecordServiceImpl();

        private readonly AntdUI.Input input_content = new AntdUI.Input();
        private readonly AntdUI.DatePicker dp_time = new AntdUI.DatePicker();
        private readonly AntdUI.Button btn_add = new AntdUI.Button();
        private readonly AntdUI.Label lblSummary = new AntdUI.Label();
        private readonly AntdUI.Table table_records = new AntdUI.Table();
        private readonly System.Windows.Forms.Panel toolbarPanel = new System.Windows.Forms.Panel();
        private readonly AntdUI.DatePicker dp_filter = new AntdUI.DatePicker();
        private readonly AntdUI.Button btn_filter_all = new AntdUI.Button();
        private readonly System.Windows.Forms.Panel filterPanel = new System.Windows.Forms.Panel();

        /// <summary>Table 行模型：属性名即列 Key</summary>
        private class MemoRow
        {
            public int Id { get; set; }
            public string 时间 { get; set; } = string.Empty;
            public string 内容 { get; set; } = string.Empty;
            public CellButton 操作 { get; }

            public MemoRow(MemoRecord record)
            {
                Id = record.Id;
                时间 = record.recordTime;
                内容 = record.content;
                操作 = new CellButton("del", I18n.T("删除", "Delete"), TTypeMini.Error);
            }
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                UpdateThemeColors();
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        private void UpdateThemeColors()
        {
            BackColor = UiTheme.PageBg;
            toolbarPanel.BackColor = UiTheme.PageBg;
            filterPanel.BackColor = UiTheme.PageBg;
            lblSummary.ForeColor = UiTheme.TextSub;
        }

        public MemoControl()
        {
            // 禁用字体自动缩放，避免容器对子控件非等比缩放
            AutoScaleMode = AutoScaleMode.None;
            BuildUi();
            this.AddListener();
            UpdateThemeColors();
            ApplyLanguage();
        }

        /// <summary>按当前语言重建表格列标题</summary>
        private void BuildColumns()
        {
            table_records.Columns.Clear();
            table_records.Columns.Add(new Column(nameof(MemoRow.时间), I18n.T("时间", "Time")) { Width = "170" });
            table_records.Columns.Add(new Column(nameof(MemoRow.内容), I18n.T("内容", "Content")));
            table_records.Columns.Add(new Column(nameof(MemoRow.操作), I18n.T("操作", "Action"), ColumnAlign.Center) { Width = "80" });
        }

        /// <summary>语言切换后重刷静态文案与列表</summary>
        public void ApplyLanguage()
        {
            input_content.PlaceholderText = I18n.T("记点什么，例如：完成了一节网课", "Write something, e.g. finished a lesson");
            dp_time.PlaceholderText = I18n.T("请选择时间", "Pick a time");
            btn_add.Text = I18n.T("添加", "Add");
            dp_filter.PlaceholderText = I18n.T("选择日期查看历史", "Filter by date");
            btn_filter_all.Text = I18n.T("全部", "All");
            table_records.EmptyText = I18n.T("暂无记录，先记一条吧", "No records yet — add one!");
            BuildColumns();
            RefreshRecords();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBg;

            // 顶部工具栏：内容输入 + 时间选择 + 添加
            toolbarPanel.Dock = DockStyle.Top;
            toolbarPanel.Height = 64;
            toolbarPanel.BackColor = UiTheme.PageBg;
            toolbarPanel.Layout += ToolbarPanel_Layout;

            input_content.PlaceholderText = I18n.T("记点什么，例如：完成了一节网课", "Write something, e.g. finished a lesson");
            input_content.Size = new Size(320, 40);
            input_content.KeyDown += input_content_KeyDown;
            toolbarPanel.Controls.Add(input_content);

            dp_time.Format = DateTimeFormat;
            dp_time.PlaceholderText = I18n.T("请选择时间", "Pick a time");
            dp_time.Size = new Size(200, 40);
            dp_time.Value = DateTime.Now;
            toolbarPanel.Controls.Add(dp_time);

            btn_add.Text = I18n.T("添加", "Add");
            btn_add.Type = TTypeMini.Primary;
            btn_add.Size = new Size(84, 40);
            btn_add.Click += btn_add_Click;
            toolbarPanel.Controls.Add(btn_add);

            // 筛选行：按日期查看历史记录，"全部"清除筛选
            filterPanel.Dock = DockStyle.Top;
            filterPanel.Height = 48;
            filterPanel.BackColor = UiTheme.PageBg;
            filterPanel.Layout += FilterPanel_Layout;

            dp_filter.Format = "yyyy-MM-dd";
            dp_filter.PlaceholderText = I18n.T("选择日期查看历史", "Filter by date");
            dp_filter.Size = new Size(180, 36);
            dp_filter.ValueChanged += dp_filter_ValueChanged;
            filterPanel.Controls.Add(dp_filter);

            btn_filter_all.Text = I18n.T("全部", "All");
            btn_filter_all.Ghost = true;
            btn_filter_all.BorderWidth = 1F;
            btn_filter_all.Size = new Size(64, 36);
            btn_filter_all.Click += btn_filter_all_Click;
            filterPanel.Controls.Add(btn_filter_all);

            lblSummary.Font = new Font("Microsoft YaHei UI", 10F);
            lblSummary.Dock = DockStyle.Top;
            lblSummary.Height = 28;
            lblSummary.TextAlign = ContentAlignment.MiddleCenter;

            table_records.Dock = DockStyle.Fill;
            table_records.EmptyText = I18n.T("暂无记录，先记一条吧", "No records yet — add one!");
            table_records.Bordered = false;
            table_records.FixedHeader = true;
            BuildColumns();
            table_records.CellButtonClick += table_records_CellButtonClick;

            // 停靠按添加的逆序处理（后添加的先停靠且更靠上）：
            // Fill 表格必须最先加（最后停靠拿剩余空间），Top 面板按视觉顺序的逆序加；
            // 若 Fill 最后加会独占整页高度、被 Top 面板盖住顶部（表格只见最后一行）
            Controls.Add(table_records);
            Controls.Add(lblSummary);
            Controls.Add(filterPanel);
            Controls.Add(toolbarPanel);
            Padding = new Padding(20, 0, 20, 16);
        }

        /// <summary>筛选行水平居中</summary>
        private void FilterPanel_Layout(object? sender, LayoutEventArgs e)
        {
            int gap = 8;
            int total = dp_filter.Width + gap + btn_filter_all.Width;
            int x = Math.Max(8, (filterPanel.Width - total) / 2);
            dp_filter.Location = new Point(x, 6);
            btn_filter_all.Location = new Point(dp_filter.Right + gap, 6);
        }

        /// <summary>工具栏水平居中，输入框宽度自适应</summary>
        private void ToolbarPanel_Layout(object? sender, LayoutEventArgs e)
        {
            int gap = 8;
            input_content.Width = Math.Clamp(toolbarPanel.Width - dp_time.Width - btn_add.Width - gap * 2 - 32, 180, 380);
            int total = input_content.Width + gap + dp_time.Width + gap + btn_add.Width;
            int x = Math.Max(8, (toolbarPanel.Width - total) / 2);
            input_content.Location = new Point(x, 12);
            dp_time.Location = new Point(x + input_content.Width + gap, 12);
            btn_add.Location = new Point(dp_time.Right + gap, 12);
        }

        private void btn_add_Click(object? sender, EventArgs e)
        {
            AddRecord();
        }

        private void input_content_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AddRecord();
                e.Handled = true;
            }
        }

        private void AddRecord()
        {
            string content = input_content.Text.Trim();
            if (content.Length == 0)
            {
                AntdUI.Message.warn(FindForm()!, I18n.T("请输入记录内容", "Please enter the content"), autoClose: 2);
                return;
            }

            // 时间默认当前时间，允许用户选择过去/未来的任意时间
            DateTime recordTime = dp_time.Value ?? DateTime.Now;
            var record = new MemoRecord
            {
                content = content,
                recordTime = recordTime.ToString(DateTimeFormat)
            };

            if (service.AddMemoRecord(record) > 0)
            {
                input_content.Text = string.Empty;
                dp_time.Value = DateTime.Now;
                RefreshRecords();
                AntdUI.Message.success(FindForm()!, I18n.T("已记录", "Saved"), autoClose: 1);
            }
            else
            {
                AntdUI.Message.error(FindForm()!, I18n.T("记录失败", "Failed to save"), autoClose: 2);
            }
        }

        private void table_records_CellButtonClick(object sender, TableButtonEventArgs e)
        {
            if (e.Record is not MemoRow row)
            {
                return;
            }

            var config = new AntdUI.Modal.Config(FindForm()!,
                I18n.T("确认删除", "Confirm deletion"),
                I18n.F("确定要删除这条记录吗？\n{0} {1}", "Delete this record?\n{0} {1}", row.时间, row.内容))
            {
                OnOk = (cfg) =>
                {
                    service.DeleteMemoById(row.Id);
                    RefreshRecords();
                    return true;
                }
            };
            AntdUI.Modal.open(config);
        }

        private void dp_filter_ValueChanged(object? sender, DateTimeNEventArgs e)
        {
            RefreshRecords();
        }

        private void btn_filter_all_Click(object? sender, EventArgs e)
        {
            bool changed = dp_filter.Value != null;
            dp_filter.Value = null;
            // 程序化赋值不一定触发 ValueChanged，手动刷新兜底
            if (changed)
            {
                RefreshRecords();
            }
        }

        private void RefreshRecords()
        {
            DateTime? filterDate = dp_filter.Value?.Date;
            List<MemoRecord> records = service.GetMemoList(filterDate);
            var rows = new AntList<MemoRow>();
            foreach (MemoRecord record in records)
            {
                rows.Add(new MemoRow(record));
            }

            table_records.Binding(rows);
            lblSummary.Text = filterDate.HasValue
                ? I18n.F("{0} · {1} 条", "{0} · {1} entries", filterDate.Value.ToString("yyyy-MM-dd"), records.Count)
                : I18n.F("今日 {0} 条 · 共 {1} 条", "{0} today · {1} total", service.GetTodayCount(), records.Count);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // 隐藏状态下窗口缩放不会触发本页布局，切回时强制重排
            if (Visible)
            {
                PerformLayout();
                RefreshRecords();
            }
        }
    }
}
