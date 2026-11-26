using System;
using System.Drawing;
using System.Windows.Forms;
using CountTimer.Model;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 节假日英雄卡：固定在列表顶部不随滚动消失，展示"下一个节日"的
    /// 大号倒计时数字 + 自上个节日以来的等待进度条。
    /// 当天开假显示"今天"，假期进行中显示还剩几天
    /// </summary>
    public class HolidayHero : CardPanel
    {
        private const int SidePad = 20;
        private const int BarHeight = 4;

        private HolidayInfo? holiday;
        private HolidayInfo? previous;
        private int daysLeft;
        private int daysLeftInHoliday;

        private readonly Font fontName = new("Microsoft YaHei UI", 16F, FontStyle.Bold);
        private readonly Font fontDate = new("Microsoft YaHei UI", 9.5F);
        private readonly Font fontDays = new("Microsoft YaHei UI", 34F, FontStyle.Bold);
        private readonly Font fontDaysUnit = new("Microsoft YaHei UI", 11F);
        private readonly Font fontToday = new("Microsoft YaHei UI", 22F, FontStyle.Bold);
        private readonly Font fontCaption = new("Microsoft YaHei UI", 8.5F);

        private static readonly string[] WeekdayEn = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public HolidayHero()
        {
            Height = 112;
        }

        /// <summary>
        /// 设置英雄卡内容。days：距首日的天数；daysInHoliday：处在多日假期之中（含首日）时还剩几天（含今天），否则为 0
        /// </summary>
        public void SetHoliday(HolidayInfo? next, int days, int daysInHoliday, HolidayInfo? prev)
        {
            holiday = next;
            previous = prev;
            daysLeft = days;
            daysLeftInHoliday = daysInHoliday;
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

            // 左上节日名 + 日期行；右上大号倒计时（数字上沿在日期行之上，日期行可以用满整行宽度）
            TextRenderer.DrawText(g, holiday.DisplayName, fontName, new Point(SidePad, 12), UiTheme.TextMain);
            TextRenderer.DrawText(g, FormatDate(holiday), fontDate, new Point(SidePad, 52), UiTheme.TextSub);
            DrawCountdown(g, ClientSize.Width);
            DrawProgress(g, ClientSize.Width);
        }

        /// <summary>日期行文本（带年份，列表跨年必须区分）：yyyy年MM月dd日 周X / yyyy-MM-dd Fri（· 标注）</summary>
        public static string FormatDate(HolidayInfo holiday)
        {
            string date, weekday;
            if (I18n.IsEnglish)
            {
                date = holiday.Date.ToString("yyyy-MM-dd");
                weekday = WeekdayEn[(int)holiday.Date.DayOfWeek];
            }
            else
            {
                date = holiday.Date.ToString("yyyy年MM月dd日");
                weekday = "周" + "日一二三四五六"[(int)holiday.Date.DayOfWeek];
            }

            return holiday.DisplayNote is null ? $"{date} {weekday}" : $"{date} {weekday} · {holiday.DisplayNote}";
        }

        private void DrawCountdown(Graphics g, int width)
        {
            // 多日假期之中：首日显示"今天"，之后显示还剩几天
            if (daysLeftInHoliday > 1)
            {
                if (daysLeft > 0)
                {
                    DrawNumber(g, width, daysLeft.ToString());
                }
                else if (daysLeft == 0)
                {
                    DrawWord(g, width, I18n.T("今天", "Today"));
                }
                else
                {
                    DrawNumber(g, width, daysLeftInHoliday.ToString());
                }

                return;
            }

            if (daysLeft <= 1)
            {
                DrawWord(g, width, daysLeft == 0 ? I18n.T("今天", "Today") : I18n.T("明天", "Tomorrow"));
                return;
            }

            DrawNumber(g, width, daysLeft.ToString());
        }

        private void DrawWord(Graphics g, int width, string word)
        {
            var size = TextRenderer.MeasureText(g, word, fontToday);
            TextRenderer.DrawText(g, word, fontToday, new Point(width - SidePad - size.Width, 16), UiTheme.HolidayAccent);
        }

        private void DrawNumber(Graphics g, int width, string number)
        {
            string unit = I18n.T("天", "days");
            var numberSize = TextRenderer.MeasureText(g, number, fontDays);
            var unitSize = TextRenderer.MeasureText(g, unit, fontDaysUnit);
            int x = width - SidePad - numberSize.Width - unitSize.Width + 4;
            TextRenderer.DrawText(g, number, fontDays, new Point(x, 10), UiTheme.HolidayAccent, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, unit, fontDaysUnit,
                new Point(x + numberSize.Width - 6, 10 + numberSize.Height - unitSize.Height - 10),
                UiTheme.TextSub, TextFormatFlags.NoPadding);
        }

        private void DrawProgress(Graphics g, int width)
        {
            int barWidth = width - SidePad * 2;
            int barY = ClientSize.Height - 18 - BarHeight;

            // 进度 = 自上个节日以来，到下一个节日之间已经过去的比例；假期进行中记满
            double ratio = 0;
            if (daysLeftInHoliday > 0)
            {
                ratio = 1;
            }
            else if (previous is not null && daysLeft >= 0)
            {
                int span = (holiday!.Date - previous.Date).Days;
                int elapsed = Math.Clamp((DateTime.Today - previous.Date).Days, 0, Math.Max(span, 1));
                ratio = span <= 0 ? 1 : (double)elapsed / span;
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

            string caption;
            if (daysLeftInHoliday > 1)
            {
                string end = I18n.IsEnglish
                    ? holiday!.LastDay.ToString("MMM d")
                    : $"{holiday!.LastDay.Month}.{holiday.LastDay.Day}";
                caption = I18n.F("假期还剩 {0} 天 · 至 {1}", "{0} days left · ends {1}", daysLeftInHoliday, end);
            }
            else if (daysLeft == 0)
            {
                caption = I18n.T("就是今天", "It's today");
            }
            else if (previous is not null)
            {
                // 每月重复的日子：上一个就是它自己的上一次，写成"自上次XX以来"才不会读成两个不同的日子
                caption = holiday!.IsMonthly && previous.Name == holiday.Name
                    ? I18n.F("自上次{0}以来已过 {1}%", "{1}% since last {0}", previous.DisplayName, Math.Round(ratio * 100))
                    : I18n.F("{0}以来已过 {1}%", "{1}% since {0}", previous.DisplayName, Math.Round(ratio * 100));
            }
            else
            {
                return;
            }

            var size = TextRenderer.MeasureText(g, caption, fontCaption);
            TextRenderer.DrawText(g, caption, fontCaption, new Point(width - SidePad - size.Width, barY - 22), UiTheme.TextSub);
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
