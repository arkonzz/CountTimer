using System;
using System.Collections.Generic;
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
    /// 节假日页签：顶部英雄卡固定展示"下一个节日"大号倒计时与等待进度，
    /// 下方自绘紧凑列表滚动展示其余节日；跨天自动刷新，主题和语言切换自动更新。
    /// 数据 = 内置节日 + 自定义生日/纪念日 + 每月重复的日子（发薪日这类）+ 法定放假与调休（后台联网更新）
    /// </summary>
    public class HolidayControl : UserControl, AntdUI.IEventListener
    {
        private readonly HolidayHero hero = new();
        private readonly HolidayList list = new();
        private readonly System.Windows.Forms.Panel toolbar = new();
        private readonly AntdUI.Button btnCustomDays = new AntdUI.Button();
        private readonly CustomDayService customDayService = new CustomDayServiceImpl();
        private readonly System.Windows.Forms.Timer dayWatcher = new() { Interval = 30_000 };
        private DateTime computedDate = DateTime.MinValue;
        private CustomDayControl? customDayEditor;

        public HolidayControl()
        {
            // 禁用字体自动缩放，避免容器对子控件非等比缩放
            AutoScaleMode = AutoScaleMode.None;
            BuildUi();
            this.AddListener();
            Rebuild(DateTime.Today);

            // 跨天（含挂起唤醒）后重算倒计时；跨年后新的一年也要联网取安排
            dayWatcher.Tick += (s, e) =>
            {
                if (Visible && DateTime.Today != computedDate)
                {
                    RebuildForToday();
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

            // 英雄卡下方一行：自定义生日 / 纪念日 / 每月重复的入口
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 46;
            toolbar.BackColor = UiTheme.PageBg;
            btnCustomDays.Ghost = true;
            btnCustomDays.BorderWidth = 1F;
            btnCustomDays.Size = new Size(220, 32);
            btnCustomDays.Location = new Point(20, 7);
            btnCustomDays.Click += btnCustomDays_Click;
            toolbar.Controls.Add(btnCustomDays);

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
            // 按钮宽度随文字：中文把三种类型写全了，比英文长
            btnCustomDays.Width = TextRenderer.MeasureText(btnCustomDays.Text, btnCustomDays.Font).Width + 36;
            Rebuild(computedDate);
        }

        private void Rebuild(DateTime today)
        {
            computedDate = today;
            List<CustomDay> customDays = customDayService.GetAll();
            var (past, upcoming) = HolidayService.GetAround(today, customDays, StatutoryHolidays.Instance.Get);

            HolidayInfo? next = upcoming.Count > 0 ? upcoming[0] : null;
            int daysLeft = next is null ? 0 : Math.Max(0, (next.Date - today.Date).Days);
            // 多日假期之中（含首日）：还剩几天（含今天）
            int daysInHoliday = next is not null && next.Date <= today.Date && next.LastDay > today.Date
                ? (next.LastDay - today.Date).Days + 1
                : 0;
            hero.SetHoliday(next, daysLeft, daysInHoliday, past.Count > 0 ? past[^1] : null);
            list.SetItems(upcoming.Skip(1).ToList(), today);
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

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // 隐藏状态下窗口缩放不会触发本页布局，切回时强制重排并补算跨天
            if (Visible)
            {
                PerformLayout();
                if (DateTime.Today != computedDate)
                {
                    RebuildForToday();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StatutoryHolidays.Instance.Updated -= OnStatutoryUpdated;
                dayWatcher.Dispose();
                customDayEditor?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
