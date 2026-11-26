using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 近 7 天专注趋势：每天一根细柱。单系列所以不用图例——旁边的统计文字说明了画的是什么。
    /// 今天用强调色，其余用弱化灰（强调今天的对比）；悬停某天时高亮那一天的槽位，
    /// 数值由统计文字显示，柱子上不逐个标数字
    /// </summary>
    public class FocusTrendControl : Control, AntdUI.IEventListener
    {
        public const int Days = 7;
        private const int BandWidth = 16;       // 每天的槽位，同时是悬停命中区域
        private const int BarWidth = 6;
        private const int BaselineY = 15;       // 柱子最高长到这里，基线画在它上面
        // 基线以下是星期标注：8 号雅黑一行要 14~16 像素，高度给少了字会被控件底边裁掉
        private const int ControlHeight = 32;
        private const int ZeroStubHeight = 2;

        private static readonly string[] WeekdayZh = { "日", "一", "二", "三", "四", "五", "六" };
        private static readonly string[] WeekdayEn = { "S", "M", "T", "W", "T", "F", "S" };

        private readonly int[] counts = new int[Days];
        private readonly Font fontWeekday = new("Microsoft YaHei UI", 8F);
        // 这批数据的第一天（6 天前）：星期标注跟着它算，和数据始终对应
        private DateTime firstDate = DateTime.Today.AddDays(-(Days - 1));
        private int hovered = -1;

        /// <summary>悬停到某一天（0 = 6 天前，Days - 1 = 今天），移开为 -1</summary>
        public event Action<int>? HoverChanged;

        /// <summary>当前悬停的那一天，含义同 <see cref="HoverChanged"/>，没悬停为 -1</summary>
        public int HoverIndex => hovered;

        public FocusTrendControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint, true);
            BackColor = UiTheme.PageBg;
            Size = new Size(Days * BandWidth, ControlHeight);
            this.AddListener();
        }

        /// <summary>values 从 6 天前到今天，依次排列</summary>
        public void SetCounts(IReadOnlyList<int> values)
        {
            for (int i = 0; i < Days; i++)
            {
                counts[i] = i < values.Count ? Math.Max(0, values[i]) : 0;
            }

            firstDate = DateTime.Today.AddDays(-(Days - 1));
            Invalidate();
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                BackColor = UiTheme.PageBg;
                Invalidate();
            }
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int max = Math.Max(1, counts.Max());

            for (int i = 0; i < Days; i++)
            {
                int bandX = i * BandWidth;
                bool isToday = i == Days - 1;
                if (i == hovered)
                {
                    using var highlight = new SolidBrush(AntdUI.Colour.HoverBg.Get());
                    g.FillRectangle(highlight, bandX, 0, BandWidth - 1, Height - 1);
                }

                // 当天没有记录就画一个贴着基线的小凸起，让这一天的槽位仍然看得见、点得到
                int barHeight = counts[i] == 0
                    ? ZeroStubHeight
                    : Math.Max(3, (int)Math.Round((BaselineY - 1) * counts[i] / (double)max));
                var bar = new Rectangle(bandX + (BandWidth - BarWidth) / 2, BaselineY - barHeight, BarWidth, barHeight);
                Color barColor = counts[i] == 0 || !isToday ? UiTheme.TrendMuted : UiTheme.HolidayAccent;
                using (var brush = new SolidBrush(barColor))
                using (GraphicsPath path = RoundedTop(bar, BarWidth / 2f))
                {
                    g.FillPath(brush, path);
                }

                DayOfWeek weekday = firstDate.AddDays(i).DayOfWeek;
                string label = I18n.IsEnglish ? WeekdayEn[(int)weekday] : WeekdayZh[(int)weekday];
                Color textColor = isToday ? UiTheme.TextMain : UiTheme.TextSub;
                var textArea = new Rectangle(bandX, BaselineY + 1, BandWidth, Height - BaselineY - 1);
                TextRenderer.DrawText(g, label, fontWeekday, textArea, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }

            // 基线：比背景深一档的细线，不抢数据的注意力
            using var baseline = new Pen(UiTheme.RingTrack, 1f);
            g.DrawLine(baseline, 0, BaselineY, Width, BaselineY);
        }

        private static GraphicsPath RoundedTop(Rectangle bounds, float radius)
        {
            // 柱子顶端圆角、底端贴基线处是直角
            var path = new GraphicsPath();
            float diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddLine(bounds.Right, bounds.Y + radius, bounds.Right, bounds.Bottom);
            path.AddLine(bounds.Right, bounds.Bottom, bounds.X, bounds.Bottom);
            path.AddLine(bounds.X, bounds.Bottom, bounds.X, bounds.Y + radius);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetHover(e.X / BandWidth);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHover(-1);
        }

        internal void SetHover(int index)
        {
            if (index < 0 || index >= Days)
            {
                index = -1;
            }

            if (index == hovered)
            {
                return;
            }

            hovered = index;
            Invalidate();
            HoverChanged?.Invoke(index);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fontWeekday.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
