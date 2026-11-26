using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.Service.ServiceImpl;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 事件倒计时管理（嵌入主窗体的 AntdUI 抽屉）：上方录入，下方列表可编辑、删除
    /// </summary>
    public class EventEditControl : UserControl, AntdUI.IEventListener
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const int FormLeft = 96;        // 输入控件左边缘
        private const int FormRight = 504;      // 输入控件右边缘

        private readonly ToDoThingService service = new ToDoThingServiceImpl();
        private bool _isEditMode;
        private ToDoThing? _editThing;
        private Dictionary<int, ToDoThing> thingsById = [];

        private readonly AntdUI.Label lblTitle = new AntdUI.Label();
        private readonly AntdUI.Label lblContent = new AntdUI.Label();
        private readonly AntdUI.Label lblTime = new AntdUI.Label();
        private readonly AntdUI.Label lblRegular = new AntdUI.Label();
        private readonly AntdUI.Input input_info = new AntdUI.Input();
        private readonly AntdUI.DatePicker dp_endtime = new AntdUI.DatePicker();
        private readonly AntdUI.Select select_regular = new AntdUI.Select();
        private readonly AntdUI.Button btn_close = new AntdUI.Button();
        private readonly AntdUI.Button btn_save = new AntdUI.Button();
        private readonly AntdUI.Table table = new AntdUI.Table();

        /// <summary>Table 行模型：属性名即列 Key</summary>
        private sealed class Row
        {
            public int Id { get; init; }
            public string Content { get; init; } = string.Empty;
            public string Time { get; init; } = string.Empty;
            public string Repeat { get; init; } = string.Empty;
            public CellButton[] Action { get; init; } = [];
        }

        private static string T(string zh, string en) => I18n.T(zh, en);

        /// <summary>条目有增删改，倒数日页据此重算</summary>
        public event Action? DataUpdated;

        public EventEditControl()
        {
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(520, 560);
            BackColor = UiTheme.PageBg;
            BuildUi();
            this.AddListener();
            ApplyLanguage();
        }

        private void BuildUi()
        {
            lblTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            lblTitle.ForeColor = UiTheme.TextMain;
            lblTitle.Location = new Point(16, 12);
            lblTitle.Size = new Size(440, 28);
            Controls.Add(lblTitle);

            lblContent.Location = new Point(16, 56);
            lblContent.Size = new Size(72, 30);
            lblContent.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblContent);

            input_info.Location = new Point(FormLeft, 52);
            input_info.Size = new Size(FormRight - FormLeft, 40);
            Controls.Add(input_info);

            lblTime.Location = new Point(16, 108);
            lblTime.Size = new Size(72, 30);
            lblTime.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblTime);

            dp_endtime.Format = DateTimeFormat;
            dp_endtime.Location = new Point(FormLeft, 104);
            dp_endtime.Size = new Size(FormRight - FormLeft, 40);
            dp_endtime.Value = DateTime.Now;
            Controls.Add(dp_endtime);

            lblRegular.Location = new Point(16, 160);
            lblRegular.Size = new Size(72, 30);
            lblRegular.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblRegular);

            select_regular.List = true;
            select_regular.Location = new Point(FormLeft, 156);
            select_regular.Size = new Size(FormRight - FormLeft, 40);
            Controls.Add(select_regular);

            btn_close.Ghost = true;
            btn_close.BorderWidth = 1F;
            btn_close.Location = new Point(FormRight - 88 - 12 - 88, 212);   // 保存按钮左侧 12px
            btn_close.Size = new Size(88, 40);
            btn_close.Click += btn_close_Click;
            Controls.Add(btn_close);

            btn_save.Type = TTypeMini.Primary;
            btn_save.Location = new Point(FormRight - 88, 212);
            btn_save.Size = new Size(88, 40);
            btn_save.Click += btn_save_Click;
            Controls.Add(btn_save);

            table.Location = new Point(16, 262);
            table.Size = new Size(Size.Width - 32, Size.Height - 262 - 16);
            table.Bordered = false;
            table.FixedHeader = true;
            table.CellButtonClick += table_CellButtonClick;
            Controls.Add(table);
        }

        /// <summary>语言切换/初始化：重刷静态文案（下拉项重建并保持选中态）</summary>
        public void ApplyLanguage()
        {
            lblTitle.Text = T("事件倒计时", "Countdown events");
            lblContent.Text = T("事件内容", "Content");
            input_info.PlaceholderText = T("请输入内容", "Enter content");
            lblTime.Text = T("事件时间", "Time");
            dp_endtime.PlaceholderText = T("请选择时间", "Pick a time");
            lblRegular.Text = T("重复", "Repeats");

            int repeatIndex = select_regular.SelectedIndex;
            select_regular.Items.Clear();
            select_regular.Items.AddRange(RepeatOptionTexts());
            select_regular.SelectedIndex = repeatIndex;

            btn_close.Text = T("取消", "Cancel");
            btn_save.Text = _isEditMode ? T("保存", "Save") : T("新增", "Add");

            // 列宽：下次要放得下"2026-11-30 09:00"，内容列占剩下的宽度
            table.EmptyText = T("还没有事件，填好后点新增", "Nothing yet — fill in the form and add");
            table.Columns.Clear();
            table.Columns.Add(new Column(nameof(Row.Content), T("内容", "Content")));
            table.Columns.Add(new Column(nameof(Row.Time), T("下次", "Next")) { Width = "140" });
            table.Columns.Add(new Column(nameof(Row.Repeat), T("重复", "Repeats")) { Width = "88" });
            table.Columns.Add(new Column(nameof(Row.Action), T("操作", "Action"), ColumnAlign.Center) { Width = "110" });
            RefreshList();
        }

        /// <summary>下拉序号即 RepeatKind 的数值：不重复 / 每天 / 工作日 / 每周</summary>
        private static object[] RepeatOptionTexts() =>
        [
            T("不重复", "Never"),
            T("每天", "Daily"),
            T("工作日", "Weekdays"),
            T("每周（同所选时间的星期）", "Weekly (same weekday)"),
        ];

        public void PrepareAddEvent()
        {
            _isEditMode = false;
            _editThing = null;
            input_info.Text = "";
            dp_endtime.MinDate = DateTime.Now;
            dp_endtime.Value = DateTime.Now;
            select_regular.SelectedIndex = 0;
            btn_save.Text = T("新增", "Add");
            RefreshList();
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
                AntdUI.Message.warn(FindForm()!, T("事件时间格式异常，请重新选择", "Invalid event time — pick again"), autoClose: 2);
            }

            select_regular.SelectedIndex = (int)thing.Kind;
            btn_save.Text = T("保存", "Save");
            RefreshList();
        }

        private void btn_close_Click(object? sender, EventArgs e)
        {
            CloseDrawer();
        }

        private void btn_save_Click(object? sender, EventArgs e)
        {
            string toDoInfo = input_info.Text.Trim();
            if (toDoInfo.Length == 0)
            {
                AntdUI.Message.warn(FindForm()!, T("请输入内容", "Please enter the content"), autoClose: 2);
                return;
            }

            if (!DateTime.TryParse(dp_endtime.Text, out DateTime endTime))
            {
                AntdUI.Message.warn(FindForm()!, T("请选择有效时间", "Please pick a valid time"), autoClose: 2);
                return;
            }

            // 下拉序号即 RepeatKind 的数值
            var kind = (RepeatKind)Math.Clamp(select_regular.SelectedIndex, 0, (int)RepeatKind.Weekly);
            if (kind == RepeatKind.None && endTime <= DateTime.Now)
            {
                AntdUI.Message.warn(FindForm()!, T("事件时间必须晚于当前时间", "Time must be later than now"), autoClose: 2);
                return;
            }

            // 重复事件从所选时间起算下一次：每周取所选日期的星期，时间取所选时刻
            if (kind != RepeatKind.None)
            {
                endTime = RepeatRule.Next(kind, (int)endTime.DayOfWeek, endTime.TimeOfDay, DateTime.Now);
            }

            if (_isEditMode)
            {
                if (_editThing == null)
                {
                    AntdUI.Message.error(FindForm()!, T("编辑数据异常", "Unexpected edit state"), autoClose: 2);
                    PrepareAddEvent();
                    return;
                }

                ApplyFields(_editThing, toDoInfo, kind, endTime);
                if (service.UpdateToDoThing(_editThing) > 0)
                {
                    AntdUI.Message.success(FindForm()!, T("修改成功", "Saved"), autoClose: 1);
                    DataUpdated?.Invoke();
                    CloseDrawer();
                }
                else
                {
                    AntdUI.Message.error(FindForm()!, T("修改失败", "Save failed"), autoClose: 2);
                }
                return;
            }

            var toDoThing = new ToDoThing { isMailed = false };
            ApplyFields(toDoThing, toDoInfo, kind, endTime);

            if (service.AddToDoThing(toDoThing) > 0)
            {
                AntdUI.Message.success(FindForm()!, T("新增成功", "Added"), autoClose: 1);
                DataUpdated?.Invoke();
                CloseDrawer();
            }
            else
            {
                AntdUI.Message.error(FindForm()!, T("新增失败", "Add failed"), autoClose: 2);
            }
        }

        /// <summary>isRegular 是"是否重复"的旧字段，保留给原有查询用；具体方式写在 repeatKind</summary>
        private static void ApplyFields(ToDoThing thing, string toDoInfo, RepeatKind kind, DateTime endTime)
        {
            thing.toDoInfo = toDoInfo;
            thing.endTime = endTime.ToString(DateTimeFormat);
            thing.isRegular = kind != RepeatKind.None;
            thing.repeatKind = (int)kind;
            thing.repeatDay = (int)endTime.DayOfWeek;
        }

        /// <summary>
        /// 关闭抽屉：内容控件的宿主只是内部容器，需要关闭 AntdUI 的抽屉层窗体
        /// （LayeredFormDrawer 关闭时会带走遮罩层）
        /// </summary>
        internal void CloseDrawer()
        {
            DrawerHelper.CloseOpenDrawers();
        }

        #region 列表

        private void table_CellButtonClick(object sender, TableButtonEventArgs e)
        {
            if (e.Record is not Row row || !thingsById.TryGetValue(row.Id, out ToDoThing? thing))
            {
                return;
            }

            if ((e.Btn as CellButton)?.Id != "del")
            {
                SetEditEvent(thing);
                return;
            }

            var config = new AntdUI.Modal.Config(FindForm()!, T("确认删除", "Confirm deletion"), I18n.F("确定要删除「{0}」吗？", "Delete \"{0}\"?", thing.toDoInfo))
            {
                OnOk = (cfg) =>
                {
                    Delete(thing);
                    return true;
                }
            };
            AntdUI.Modal.open(config);
        }

        private void Delete(ToDoThing thing)
        {
            service.deleteById(thing.Id);
            // 正在编辑的就是被删掉的这条：录入区回到新增状态
            if (_editThing?.Id == thing.Id)
            {
                PrepareAddEvent();
            }

            RefreshList();
            DataUpdated?.Invoke();
        }

        private void RefreshList()
        {
            List<ToDoThing> things = service.GetTodoList();
            thingsById = things.ToDictionary(thing => thing.Id);
            DateTime now = DateTime.Now;

            var rows = new BindingList<Row>(things.Select(thing => new Row
            {
                Id = thing.Id,
                Content = thing.toDoInfo,
                // 循环事件存的下一次可能已经过去（后台还没滚），按规则现算
                Time = RepeatRule.NextOccurrence(thing, now)?.ToString("yyyy-MM-dd HH:mm") ?? "—",
                Repeat = RepeatText(thing),
                Action =
                [
                    new CellButton("edit", T("编辑", "Edit"), TTypeMini.Primary),
                    new CellButton("del", T("删除", "Delete"), TTypeMini.Error),
                ],
            }).ToList());

            table.Binding(rows);
        }

        /// <summary>列表"重复"一列：不重复 / 每天 / 工作日 / 每周三</summary>
        private static string RepeatText(ToDoThing thing)
        {
            (string zh, string en) = RepeatRule.Describe(thing.Kind, (DayOfWeek)thing.repeatDay);
            return zh.Length == 0 ? T("不重复", "Never") : T(zh, en);
        }

        #endregion

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                BackColor = UiTheme.PageBg;
                lblTitle.ForeColor = UiTheme.TextMain;
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();
    }
}
