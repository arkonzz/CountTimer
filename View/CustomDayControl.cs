using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 生日 / 纪念日管理（嵌入主窗体的 AntdUI 抽屉）：上方录入，下方列表可编辑、删除
    /// </summary>
    public class CustomDayControl : UserControl, AntdUI.IEventListener
    {
        private const int FormLeft = 96;        // 输入控件左边缘
        private const int FormRight = 504;      // 输入控件右边缘
        private static readonly string[] MonthNamesEn = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        private readonly CustomDayService service = new CustomDayServiceImpl();
        private readonly List<(AntdUI.Label label, string zh, string en)> labels = [];
        private Dictionary<int, CustomDay> daysById = [];
        private CustomDay? editing;

        private readonly AntdUI.Label lblTitle = new AntdUI.Label();
        private readonly AntdUI.Label lblName = new AntdUI.Label();
        private readonly AntdUI.Label lblKind = new AntdUI.Label();
        private readonly AntdUI.Label lblMonth = new AntdUI.Label();
        private readonly AntdUI.Label lblDay = new AntdUI.Label();
        private readonly AntdUI.Label lblYear = new AntdUI.Label();
        private readonly AntdUI.Input input_name = new AntdUI.Input();
        private readonly AntdUI.Select select_kind = new AntdUI.Select();
        private readonly AntdUI.Select select_calendar = new AntdUI.Select();
        private readonly AntdUI.InputNumber num_month = new AntdUI.InputNumber();
        private readonly AntdUI.InputNumber num_day = new AntdUI.InputNumber();
        private readonly AntdUI.Input input_year = new AntdUI.Input();
        private readonly AntdUI.Button btn_close = new AntdUI.Button();
        private readonly AntdUI.Button btn_save = new AntdUI.Button();
        private readonly AntdUI.Table table = new AntdUI.Table();

        /// <summary>Table 行模型：属性名即列 Key</summary>
        private sealed class Row
        {
            public int Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public string Date { get; init; } = string.Empty;
            public string Next { get; init; } = string.Empty;
            public CellButton[] Action { get; init; } = [];
        }

        /// <summary>条目有增删改，节假日页据此重算</summary>
        public event Action? DataUpdated;

        public CustomDayControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            BuildUi();
            this.AddListener();
            ApplyLanguage();
        }

        private static string T(string zh, string en) => I18n.T(zh, en);

        #region 布局

        private void BuildUi()
        {
            Size = new Size(520, 600);
            BackColor = UiTheme.PageBg;

            lblTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            lblTitle.ForeColor = UiTheme.TextMain;
            lblTitle.Location = new Point(16, 12);
            lblTitle.Size = new Size(440, 28);
            Controls.Add(lblTitle);

            // 字段名统一在左侧右对齐
            AddFieldLabel(lblName, y: 52, "名称", "Name");
            input_name.Location = new Point(FormLeft, 52);
            input_name.Size = new Size(FormRight - FormLeft, 40);
            Controls.Add(input_name);

            AddFieldLabel(lblKind, y: 100, "类型", "Type");
            int half = (FormRight - FormLeft - 12) / 2;
            select_kind.List = true;
            select_kind.Location = new Point(FormLeft, 100);
            select_kind.Size = new Size(half, 40);
            Controls.Add(select_kind);

            select_calendar.List = true;
            select_calendar.Location = new Point(FormLeft + half + 12, 100);
            select_calendar.Size = new Size(FormRight - FormLeft - half - 12, 40);
            Controls.Add(select_calendar);

            // 月 / 日 / 年份：小字标题放在输入框上方，三等分。年份是普通输入框，留空表示不填
            var numberFields = new (AntdUI.Label Label, string Zh, string En, AntdUI.InputNumber Input, int Maximum)[]
            {
                (lblMonth, "月", "Month", num_month, 12),
                (lblDay, "日", "Day", num_day, 31),
            };
            int cellWidth = (FormRight - FormLeft - 24) / 3;
            for (int i = 0; i < numberFields.Length; i++)
            {
                var (label, zh, en, input, maximum) = numberFields[i];
                AddCaption(label, zh, en, i, cellWidth);

                input.Minimum = 1;
                input.Maximum = maximum;
                input.Increment = 1;
                input.DecimalPlaces = 0;
                input.InterceptArrowKeys = true;
                input.Location = new Point(FormLeft + i * (cellWidth + 12), 166);
                input.Size = new Size(cellWidth, 36);
                Controls.Add(input);
            }

            AddCaption(lblYear, "年份（选填）", "Year (optional)", 2, cellWidth);
            input_year.Location = new Point(FormLeft + 2 * (cellWidth + 12), 166);
            input_year.Size = new Size(cellWidth, 36);
            Controls.Add(input_year);

            btn_close.Ghost = true;
            btn_close.BorderWidth = 1F;
            btn_close.Location = new Point(FormRight - 88 - 12 - 88, 210);   // 保存按钮左侧 12px
            btn_close.Size = new Size(88, 40);
            btn_close.Click += (s, e) => CloseDrawer();
            Controls.Add(btn_close);

            btn_save.Type = TTypeMini.Primary;
            btn_save.Location = new Point(FormRight - 88, 210);
            btn_save.Size = new Size(88, 40);
            btn_save.Click += btn_save_Click;
            Controls.Add(btn_save);

            table.Location = new Point(16, 262);
            table.Size = new Size(Size.Width - 32, Size.Height - 262 - 16);
            table.Bordered = false;
            table.FixedHeader = true;
            table.CellButtonClick += table_CellButtonClick;
            Controls.Add(table);

            // 初始化赋值完成后再挂事件：农历每月最多三十天
            select_calendar.SelectedValueChanged += (s, e) => num_day.Maximum = select_calendar.SelectedIndex == 1 ? 30 : 31;
        }

        private void AddCaption(AntdUI.Label label, string zh, string en, int index, int cellWidth)
        {
            label.Font = new Font("Microsoft YaHei UI", 8.5F);
            label.ForeColor = UiTheme.TextSub;
            label.Location = new Point(FormLeft + index * (cellWidth + 12), 148);
            label.Size = new Size(cellWidth, 16);
            labels.Add((label, zh, en));
            Controls.Add(label);
        }

        private void AddFieldLabel(AntdUI.Label label, int y, string zh, string en)
        {
            label.Location = new Point(16, y);
            label.Size = new Size(FormLeft - 8 - 16, 40);
            label.TextAlign = ContentAlignment.MiddleRight;
            labels.Add((label, zh, en));
            Controls.Add(label);
        }

        /// <summary>打开抽屉前调用：清空录入区并刷新列表</summary>
        public void Prepare()
        {
            ResetForm();
            RefreshList();
        }

        #endregion

        #region 语言与主题

        /// <summary>语言切换 / 初始化：重刷文字（下拉项重建并保持选中态）</summary>
        public void ApplyLanguage()
        {
            lblTitle.Text = T("生日 / 纪念日", "Special days");
            foreach ((AntdUI.Label label, string zh, string en) in labels)
            {
                label.Text = T(zh, en);
            }

            input_name.PlaceholderText = T("例如：妈妈生日", "e.g. Mom's birthday");
            input_year.PlaceholderText = T("不填", "None");
            RebuildOptions(select_kind, T("生日", "Birthday"), T("纪念日", "Anniversary"));
            RebuildOptions(select_calendar, T("公历", "Solar"), T("农历", "Lunar"));
            btn_close.Text = T("关闭", "Close");
            btn_save.Text = editing is null ? T("新增", "Add") : T("保存", "Save");
            table.EmptyText = T("还没有添加，填好后点新增", "Nothing yet — fill in the form and add");

            table.Columns.Clear();
            table.Columns.Add(new Column(nameof(Row.Name), T("名称", "Name")));
            table.Columns.Add(new Column(nameof(Row.Date), T("日期", "Date")) { Width = "150" });
            table.Columns.Add(new Column(nameof(Row.Next), T("下次", "Next")) { Width = "100" });
            table.Columns.Add(new Column(nameof(Row.Action), T("操作", "Action"), ColumnAlign.Center) { Width = "120" });
            RefreshList();
        }

        private static void RebuildOptions(AntdUI.Select select, params object[] items)
        {
            int selected = select.SelectedIndex;
            select.Items.Clear();
            select.Items.AddRange(items);
            select.SelectedIndex = selected < 0 ? 0 : selected;
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                BackColor = UiTheme.PageBg;
                // 字段名和输入框上方的小标题都是显式指定的文字色，要跟着主题换
                lblTitle.ForeColor = UiTheme.TextMain;
                foreach ((AntdUI.Label label, string zh, string en) in labels)
                {
                    label.ForeColor = UiTheme.TextSub;
                }
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        #endregion

        #region 录入

        private void ResetForm()
        {
            editing = null;
            input_name.Text = string.Empty;
            select_kind.SelectedIndex = 0;
            select_calendar.SelectedIndex = 0;
            num_month.Value = DateTime.Today.Month;
            num_day.Value = DateTime.Today.Day;
            input_year.Text = string.Empty;
            btn_save.Text = T("新增", "Add");
        }

        private void Edit(CustomDay day)
        {
            editing = day;
            input_name.Text = day.title;
            select_kind.SelectedIndex = day.kind == CustomDay.KindAnniversary ? 1 : 0;
            select_calendar.SelectedIndex = day.isLunar ? 1 : 0;
            num_month.Value = day.month;
            num_day.Value = Math.Min(day.day, (int)(num_day.Maximum ?? 31));
            input_year.Text = day.year > 0 ? day.year.ToString() : string.Empty;
            btn_save.Text = T("保存", "Save");
        }

        private void btn_save_Click(object? sender, EventArgs e)
        {
            string title = input_name.Text.Trim();
            if (title.Length == 0)
            {
                AntdUI.Message.warn(FindForm()!, T("请输入名称", "Please enter a name"), autoClose: 2);
                return;
            }

            bool isLunar = select_calendar.SelectedIndex == 1;
            int month = (int)num_month.Value, day = (int)num_day.Value;
            string yearText = input_year.Text.Trim();
            string? problem = (month, day, yearText) switch
            {
                ( < 1, _, _) or (_, < 1, _) => T("请填写月份和日期", "Please fill in the month and day"),
                (_, _, not "") when !int.TryParse(yearText, out int parsed) || parsed < 0 => T("年份需要是数字", "Year must be a number"),
                _ => null,
            };
            if (problem is not null)
            {
                AntdUI.Message.warn(FindForm()!, problem, autoClose: 2);
                return;
            }

            int year = yearText.Length == 0 ? 0 : int.Parse(yearText);
            problem = year switch
            {
                > 0 when year > DateTime.Today.Year => T("年份不能晚于今年", "Year can't be later than this year"),
                > 0 when isLunar && year < 1901 => T("农历年份需在 1901 年之后", "Lunar years start from 1901"),
                _ => null,
            };
            if (problem is not null)
            {
                AntdUI.Message.warn(FindForm()!, problem, autoClose: 2);
                return;
            }

            // 农历三十在当月没有的年份、以及 2 月 29 日，由换算兜底落到当月最后一天
            CustomDay target = editing ?? new CustomDay();
            target.title = title;
            target.kind = select_kind.SelectedIndex == 1 ? CustomDay.KindAnniversary : CustomDay.KindBirthday;
            target.isLunar = isLunar;
            target.month = month;
            target.day = day;
            target.year = year;

            bool saved = editing is null ? service.Add(target) > 0 : service.Update(target) > 0;
            if (!saved)
            {
                AntdUI.Message.error(FindForm()!, T("保存失败", "Failed to save"), autoClose: 2);
                return;
            }

            AntdUI.Message.success(FindForm()!, T("已保存", "Saved"), autoClose: 1);
            ResetForm();
            RefreshList();
            DataUpdated?.Invoke();
        }

        #endregion

        #region 列表

        private void table_CellButtonClick(object sender, TableButtonEventArgs e)
        {
            if (e.Record is not Row row || !daysById.TryGetValue(row.Id, out CustomDay? day))
            {
                return;
            }

            if ((e.Btn as CellButton)?.Id != "del")
            {
                Edit(day);
                return;
            }

            var config = new AntdUI.Modal.Config(FindForm()!, T("确认删除", "Confirm deletion"), I18n.F("确定要删除「{0}」吗？", "Delete \"{0}\"?", day.title))
            {
                OnOk = (cfg) =>
                {
                    service.Delete(day.Id);
                    if (editing?.Id == day.Id)
                    {
                        ResetForm();
                    }

                    RefreshList();
                    DataUpdated?.Invoke();
                    return true;
                }
            };
            AntdUI.Modal.open(config);
        }

        private void RefreshList()
        {
            List<CustomDay> days = service.GetAll();
            daysById = days.ToDictionary(day => day.Id);
            DateTime today = DateTime.Today;

            var rows = new BindingList<Row>(days.Select(day => new Row
            {
                Id = day.Id,
                Name = day.title,
                Date = Describe(day),
                Next = HolidayService.NextCustomDay(day, today)?.ToString("yyyy-MM-dd") ?? "—",
                Action =
                [
                    new CellButton("edit", T("编辑", "Edit"), TTypeMini.Primary),
                    new CellButton("del", T("删除", "Delete"), TTypeMini.Error),
                ],
            }).ToList());

            table.Binding(rows);
        }

        /// <summary>"农历 8月1日 · 1990年" / "Solar Jan 20" / "Lunar 8/1 · 1990"</summary>
        private static string Describe(CustomDay day)
        {
            string calendar = day.isLunar ? T("农历", "Lunar") : T("", "Solar");
            string date = I18n.IsEnglish
                ? (day.isLunar ? $"{day.month}/{day.day}" : $"{MonthNamesEn[day.month - 1]} {day.day}")
                : $"{day.month}月{day.day}日";
            string year = day.year > 0 ? (I18n.IsEnglish ? $" · {day.year}" : $" · {day.year}年") : string.Empty;
            return (calendar.Length > 0 ? calendar + " " : string.Empty) + date + year;
        }

        internal void CloseDrawer() => DrawerHelper.CloseOpenDrawers();

        #endregion
    }
}
