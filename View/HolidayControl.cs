using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Service;

namespace CountTimer.View
{
    /// <summary>
    /// 节假日页签：顶部英雄卡固定展示"下一个节日"大号倒计时与等待进度，
    /// 下方自绘紧凑列表滚动展示其余节日；跨天自动刷新，主题切换自动重配色
    /// </summary>
    public class HolidayControl : UserControl, AntdUI.IEventListener
    {
        private readonly HolidayHero hero = new();
        private readonly HolidayList list = new();
        private readonly System.Windows.Forms.Timer dayWatcher = new() { Interval = 30_000 };
        private DateTime computedDate = DateTime.MinValue;

        public HolidayControl()
        {
            // 禁用字体自动缩放，避免容器对子控件非等比缩放
            AutoScaleMode = AutoScaleMode.None;
            BuildUi();
            this.AddListener();
            Rebuild(DateTime.Today);

            // 跨天（含挂起唤醒）后重算倒计时
            dayWatcher.Tick += (s, e) =>
            {
                if (Visible && DateTime.Today != computedDate)
                {
                    Rebuild(DateTime.Today);
                }
            };
            dayWatcher.Start();
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                BackColor = UiTheme.PageBg;
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
            // 停靠按添加的逆序处理：Fill 的列表先加，Top 的英雄卡后加（同小计页）
            Controls.Add(list);
            Controls.Add(hero);
            Padding = new Padding(16, 12, 16, 12);
        }

        private void Rebuild(DateTime today)
        {
            computedDate = today;
            var (past, upcoming) = HolidayService.GetAround(today);

            hero.SetHoliday(
                upcoming.Count > 0 ? upcoming[0] : null,
                upcoming.Count > 0 ? (upcoming[0].Date.Date - today.Date).Days : 0,
                past.Count > 0 ? past[^1] : null);

            // 第一个节日已由英雄卡展示，列表只放其余的
            var entries = new List<(HolidayInfo, int)>();
            for (int i = 1; i < upcoming.Count; i++)
            {
                entries.Add((upcoming[i], (upcoming[i].Date.Date - today.Date).Days));
            }
            list.SetItems(entries);
        }

        /// <summary>语言切换后整页重算（节日名/农历标注/倒计时文案全部重建）</summary>
        public void ApplyLanguage()
        {
            Rebuild(computedDate);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            // 隐藏状态下窗口缩放不会触发本页布局，切回时强制重排并补算跨天
            if (Visible)
            {
                PerformLayout();
                if (DateTime.Today != computedDate)
                {
                    Rebuild(DateTime.Today);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dayWatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
