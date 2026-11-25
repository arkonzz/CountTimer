using System;
using System.Drawing;
using System.Windows.Forms;
using CountTimer.Model;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 节假日英雄卡：固定在列表顶部不随滚动消失，展示"下一个节日"的
    /// 大号倒计时数字 + 自上个节日以来的等待进度条；当天即节日时显示"今天"
    /// </summary>
    public class HolidayHero : CardPanel
    {
        private const int SidePad = 20;
        private const int BarHeight = 4;

        private HolidayInfo? holiday;
        private HolidayInfo? previous;
        private int daysLeft;

        private readonly Font fontName = new("Microsoft YaHei UI", 16F, FontStyle.Bold);
        private readonly Font fontDate = new("Microsoft YaHei UI", 9.5F);
        private readonly Font fontDays = new("Microsoft YaHei UI", 34F, FontStyle.Bold);
        private readonly Font fontDaysUnit = new("Microsoft YaHei UI", 11F);
        private readonly Font fontToday = new("Microsoft YaHei UI", 22F, FontStyle.Bold);
        private readonly Font fontCaption = new("Microsoft YaHei UI", 8.5F);

        public HolidayHero()
        {
            Height = 112;
        }

        /// <summary>设置英雄卡内容；daysLeft 为 0 表示当天就是该节日</summary>
        public void SetHoliday(HolidayInfo? next, int days, HolidayInfo? prev)
        {
            holiday = next;
            previous = prev;
            daysLeft = days;
            Invalidate();
        }

        protected override void OnThemeChanged() => Invalidate();

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (holiday is null)
            {
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int width = ClientSize.Width;

            // 左上：节日名 + 日期行；右上：大号倒计时（不与底部进度说明重叠）
            TextRenderer.DrawText(g, holiday.DisplayName, fontName, new Point(SidePad, 12), UiTheme.TextMain);
            TextRenderer.DrawText(g, FormatDate(holiday), fontDate, new Point(SidePad, 52), UiTheme.TextSub);
            DrawCountdown(g, width);
            DrawProgress(g, width);
        }

        private static readonly string[] WeekdayEn = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        /// <summary>日期行文本（带年份，列表跨年必须区分）：yyyy年MM月dd日 周X / yyyy-MM-dd Fri（· 农历/节气标注）</summary>
        public static string FormatDate(HolidayInfo holiday)
        {
            if (I18n.IsEnglish)
            {
                string weekday = WeekdayEn[(int)holiday.Date.DayOfWeek];
                string date = holiday.Date.ToString("yyyy-MM-dd");
                return holiday.DisplayNote is null ? $"{date} {weekday}" : $"{date} {weekday} · {holiday.DisplayNote}";
            }

            string weekdayZh = "周" + "日一二三四五六"[(int)holiday.Date.DayOfWeek];
            return holiday.DisplayNote is null
                ? $"{holiday.Date:yyyy年MM月dd日} {weekdayZh}"
                : $"{holiday.Date:yyyy年MM月dd日} {weekdayZh} · {holiday.DisplayNote}";
        }

        private void DrawCountdown(Graphics g, int width)
        {
            if (daysLeft <= 1)
            {
                string text = daysLeft == 0 ? I18n.T("今天", "Today") : I18n.T("明天", "Tomorrow");
                var size = TextRenderer.MeasureText(g, text, fontToday);
                TextRenderer.DrawText(g, text, fontToday, new Point(width - SidePad - size.Width, 16), UiTheme.HolidayAccent);
                return;
            }

            string number = daysLeft.ToString();
            var numSize = TextRenderer.MeasureText(g, number, fontDays);
            var unitSize = TextRenderer.MeasureText(g, I18n.T("天", "days"), fontDaysUnit);
            int x = width - SidePad - numSize.Width - unitSize.Width + 4;
            TextRenderer.DrawText(g, number, fontDays, new Point(x, 10), UiTheme.HolidayAccent, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, I18n.T("天", "days"), fontDaysUnit,
                new Point(x + numSize.Width - 6, 10 + numSize.Height - unitSize.Height - 10),
                UiTheme.TextSub, TextFormatFlags.NoPadding);
        }

        private void DrawProgress(Graphics g, int width)
        {
            int barWidth = width - SidePad * 2;
            int barY = ClientSize.Height - 18 - BarHeight;

            double ratio = 1;
            string caption;
            if (daysLeft == 0)
            {
                caption = I18n.T("就是今天", "It's today");
            }
            else if (previous is null)
            {
                caption = string.Empty;
                ratio = 0;
            }
            else
            {
                int span = (holiday!.Date.Date - previous.Date.Date).Days;
                int elapsed = Math.Clamp((DateTime.Today - previous.Date.Date).Days, 0, span);
                ratio = span <= 0 ? 1 : (double)elapsed / span;
                caption = I18n.F("{0}以来已过 {1}%", "{1}% since {0}", previous.DisplayName, Math.Round(ratio * 100));
            }

            var track = new Rectangle(SidePad, barY, barWidth, BarHeight);
            using (var path = RoundedRect(track, BarHeight / 2))
            {
                using var brush = new SolidBrush(UiTheme.RingTrack);
                g.FillPath(brush, path);
            }

            if (ratio > 0)
            {
                var fill = new Rectangle(SidePad, barY, Math.Max(BarHeight, (int)(barWidth * ratio)), BarHeight);
                using var path = RoundedRect(fill, BarHeight / 2);
                using var brush = new SolidBrush(UiTheme.HolidayAccent);
                g.FillPath(brush, path);
            }

            if (caption.Length > 0)
            {
                var size = TextRenderer.MeasureText(g, caption, fontCaption);
                TextRenderer.DrawText(g, caption, fontCaption, new Point(width - SidePad - size.Width, barY - 24), UiTheme.TextSub);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
