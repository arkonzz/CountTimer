using System;
using System.Collections.Generic;
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
    /// 倒数日页签：顶部英雄卡固定展示"下一个日子"大号倒计时与等待进度，
    /// 下方自绘紧凑列表滚动展示其余条目；跨天自动刷新，主题和语言切换自动更新。
    /// 数据 = 内置节日 + 自定义生日/纪念日 + 每月重复的日子（发薪日这类）+ 事件倒计时
    /// + 法定放假与调休（后台联网更新）。事件归这一页管：到点托盘提醒、循环事件滚动、过期事件清理都在这里
    /// </summary>
    public class HolidayControl : UserControl, AntdUI.IEventListener
    {
        private static readonly TimeSpan DuplicateNotificationWindow = TimeSpan.FromSeconds(10);

        private readonly HolidayHero hero = new();
        private readonly HolidayList list = new();
        private readonly System.Windows.Forms.Panel toolbar = new();
        private readonly AntdUI.Button btnCustomDays = new AntdUI.Button();
        private readonly AntdUI.Button btnEvents = new AntdUI.Button();
        private readonly CustomDayService customDayService = new CustomDayServiceImpl();
        private readonly ToDoThingService todoService = new ToDoThingServiceImpl();
        private readonly ExpiredEventCleanupWorker eventWorker = new();
        private readonly Action<string, string> showBalloon;
        private readonly System.Windows.Forms.Timer dayWatcher = new() { Interval = 30_000 };
        private DateTime computedDate = DateTime.MinValue;
        private CustomDayControl? customDayEditor;
        private EventEditControl? eventEditor;
        // 上次重算时列表里有事件：后台会清理、滚动事件，当天之内的还要更新剩余时间，要定期跟着重算
        private bool showsEvents;
        // 到点提醒去重：同一次发生只提醒一遍
        private readonly HashSet<string> notifiedOccurrences = [];
        private string? lastNotificationText;
        private DateTime lastNotificationTime = DateTime.MinValue;

        public HolidayControl(Action<string, string> showBalloon)
        {
            this.showBalloon = showBalloon;

            // 禁用字体自动缩放，避免容器对子控件非等比缩放
            AutoScaleMode = AutoScaleMode.None;
            BuildUi();
            this.AddListener();

            // 事件的后台维护：到点提醒、循环事件滚到下一次、清理过期的一次性事件。
            // 先把关机期间错过的处理掉（循环的滚到下一次、一次性的删掉），否则一启动就会补一串过期的提醒
            todoService.updateRegularEvent();
            todoService.deleteByTime(DateTime.Now);
            eventWorker.Start(RefreshEventsFromWorker, NotifyEventFromWorker);

            Rebuild(DateTime.Today);

            // 跨天（含挂起唤醒）后重算倒计时；跨年后新的一年也要联网取安排
            dayWatcher.Tick += (s, e) =>
            {
                if (!Visible)
                {
                    return;
                }

                if (DateTime.Today != computedDate)
                {
                    RebuildForToday();
                }
                else if (showsEvents)
                {
                    Rebuild(computedDate);
                }
            };
            dayWatcher.Start();

            // 法定节假日安排在后台联网更新，有变化就重算
            StatutoryHolidays.Instance.Updated += OnStatutoryUpdated;
            StatutoryHolidays.Instance.RefreshSoon(DateTime.Today.Year);
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                // 工具栏的背景也要跟着换：上面的幽灵按钮透出的是父面板的底色
                BackColor = UiTheme.PageBg;
                toolbar.BackColor = UiTheme.PageBg;
                toolbar.Invalidate(true);
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            BackColor = UiTheme.PageBg;

            hero.Dock = DockStyle.Top;
            list.Dock = DockStyle.Fill;

            // 英雄卡下方一行：自定义生日 / 纪念日 / 每月重复、事件倒计时两个入口
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 46;
            toolbar.BackColor = UiTheme.PageBg;
            btnCustomDays.Ghost = true;
            btnCustomDays.BorderWidth = 1F;
            btnCustomDays.Size = new Size(220, 32);
            btnCustomDays.Location = new Point(20, 7);
            btnCustomDays.Click += btnCustomDays_Click;
            toolbar.Controls.Add(btnCustomDays);

            btnEvents.Ghost = true;
            btnEvents.BorderWidth = 1F;
            btnEvents.Size = new Size(140, 32);
            btnEvents.Location = new Point(btnCustomDays.Right + 10, 7);
            btnEvents.Click += btnEvents_Click;
            toolbar.Controls.Add(btnEvents);

            // 停靠按添加的逆序处理：Fill 的列表先加，Top 面板按视觉顺序的逆序加（同小计页）
            Controls.Add(list);
            Controls.Add(toolbar);
            Controls.Add(hero);
            Padding = new Padding(16, 12, 16, 12);
        }

        /// <summary>语言切换后整页重算（文案随语言重建）</summary>
        public void ApplyLanguage()
        {
            btnCustomDays.Text = I18n.T("+ 生日 / 纪念日 / 每月重复", "+ Special days");
            btnEvents.Text = I18n.T("+ 事件倒计时", "+ Event");
            // 按钮宽度随文字：中文把三种类型写全了，比英文长
            btnCustomDays.Width = TextRenderer.MeasureText(btnCustomDays.Text, btnCustomDays.Font).Width + 36;
            btnEvents.Width = TextRenderer.MeasureText(btnEvents.Text, btnEvents.Font).Width + 36;
            btnEvents.Left = btnCustomDays.Right + 10;
            eventEditor?.ApplyLanguage();
            Rebuild(computedDate);
        }

        private void Rebuild(DateTime today)
        {
            computedDate = today;
            List<CustomDay> customDays = customDayService.GetAll();
            List<ToDoThing> events = todoService.GetTodoList();
            showsEvents = events.Count > 0;
            var (past, upcoming) = HolidayService.GetAround(today, customDays, StatutoryHolidays.Instance.Get, events);

            // 每天这类循环事件永远是"今天 / 明天"，不上英雄卡，只在列表里
            HolidayInfo? next = HolidayService.PickHero(upcoming);
            int daysLeft = next is null ? 0 : Math.Max(0, (next.Date - today.Date).Days);
            // 多日假期之中（含首日）：还剩几天（含今天）
            int daysInHoliday = next is not null && next.Date <= today.Date && next.LastDay > today.Date
                ? (next.LastDay - today.Date).Days + 1
                : 0;
            // 今天之内还没到点的事件：大卡片上写还剩多久，比"就是今天"有用
            string? todayCaption = next is null || HolidayService.TimeLeftToday(next, DateTime.Now) is not { } left
                ? null
                : I18n.T(left.Zh, left.En);
            hero.SetHoliday(next, daysLeft, daysInHoliday, past.Count > 0 ? past[^1] : null, todayCaption);
            list.SetItems(upcoming.Where(h => !ReferenceEquals(h, next)).ToList(), today);
        }

        /// <summary>按"今天"重算，并顺带触发一次联网更新（内部有 12 小时节流）</summary>
        private void RebuildForToday()
        {
            Rebuild(DateTime.Today);
            StatutoryHolidays.Instance.RefreshSoon(DateTime.Today.Year);
        }

        private void OnStatutoryUpdated()
        {
            // 后台线程触发，切回界面线程重算
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke((Action)(() => Rebuild(computedDate)));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void btnCustomDays_Click(object? sender, EventArgs e)
        {
            customDayEditor ??= new CustomDayControl();
            // 先取消订阅，避免重复订阅
            customDayEditor.DataUpdated -= OnCustomDaysChanged;
            customDayEditor.DataUpdated += OnCustomDaysChanged;
            customDayEditor.Prepare();

            AntdUI.Drawer.open(new AntdUI.Drawer.Config(FindForm()!, customDayEditor)
            {
                Align = AntdUI.TAlignMini.Right,
                Mask = true,
                MaskClosable = true,
                Dispose = false
            });
        }

        private void OnCustomDaysChanged() => Rebuild(computedDate);

        #region 事件：到点提醒与后台维护

        /// <summary>后台线程触发：列表里有事件时切回界面线程重算（刚清理、滚动过，或剩余时间该变了）</summary>
        private void RefreshEventsFromWorker()
        {
            if (showsEvents)
            {
                RunOnUi(() =>
                {
                    if (showsEvents)
                    {
                        Rebuild(computedDate);
                    }
                });
            }
        }

        private void NotifyEventFromWorker(ToDoThing toDoThing) => RunOnUi(() => NotifyEventEnded(toDoThing));

        private void NotifyEventEnded(ToDoThing toDoThing)
        {
            if (!notifiedOccurrences.Add($"{toDoThing.Id}:{toDoThing.endTime}"))
            {
                return;
            }

            string notificationText = string.IsNullOrWhiteSpace(toDoThing.toDoInfo)
                ? I18n.T("时间到！", "Time's up!")
                : I18n.F("{0} 事件结束", "{0} event ended", toDoThing.toDoInfo);
            // 同一条文案短时间内不重复弹
            DateTime now = DateTime.Now;
            if (lastNotificationText == notificationText && now - lastNotificationTime < DuplicateNotificationWindow)
            {
                return;
            }

            lastNotificationText = notificationText;
            lastNotificationTime = now;
            showBalloon(I18n.T("倒计时结束", "Countdown finished"), notificationText);
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
            }
        }

        #endregion

        #region E2E 自测（仅 CT_E2E=1 时由主窗体调用）

        internal void E2EOpenEventDrawer() => btnEvents_Click(null, EventArgs.Empty);

        internal void E2ECloseDrawer() => DrawerHelper.CloseOpenDrawers();

        #endregion

        /// <summary>打开事件倒计时抽屉：上方录入，下方列表可编辑、删除</summary>
        private void btnEvents_Click(object? sender, EventArgs e)
        {
            eventEditor ??= new EventEditControl();
            // 先取消订阅，避免重复订阅
            eventEditor.DataUpdated -= OnEventsChanged;
            eventEditor.DataUpdated += OnEventsChanged;
            eventEditor.PrepareAddEvent();

            AntdUI.Drawer.open(new AntdUI.Drawer.Config(FindForm()!, eventEditor)
            {
                Align = AntdUI.TAlignMini.Right,
                Mask = true,
                MaskClosable = true,
                Dispose = false
            });
        }

        private void OnEventsChanged()
        {
            Rebuild(computedDate);
            // 新加的事件可能马上就到点：让后台立刻重排下一次检查的时间
            eventWorker.Wake();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // 隐藏状态下窗口缩放不会触发本页布局，切回时强制重排
            if (Visible)
            {
                PerformLayout();
                // 后台可能清理、滚动过事件：切回来就整页重算；跨天则顺带联网更新放假安排
                if (DateTime.Today != computedDate)
                {
                    RebuildForToday();
                }
                else
                {
                    Rebuild(DateTime.Today);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StatutoryHolidays.Instance.Updated -= OnStatutoryUpdated;
                dayWatcher.Dispose();
                eventWorker.Dispose();
                customDayEditor?.Dispose();
                eventEditor?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
