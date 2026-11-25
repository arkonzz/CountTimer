using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AntdUI;
using CountTimer.Model;
using CountTimer.Share;

namespace CountTimer.View
{
    /// <summary>
    /// 紧凑节日列表（自绘）：圆角行卡 + 主题色板细条滚动条（支持滚轮/拖拽），
    /// 替代 FlowLayoutPanel 以摆脱明暗主题下都突兀的原生滚动条
    /// </summary>
    public class HolidayList : Control, AntdUI.IEventListener
    {
        private const int ItemHeight = 64;
        private const int ItemGap = 10;
        private const int SidePad = 18;
        private const int BarWidth = 4;
        private const int BarGutter = 10;
        private const int WheelStep = 96;

        private sealed record Item(string Name, string DateText, string DaysText);

        private readonly List<Item> items = [];
        private int scrollTop;
        private bool draggingBar;
        private int dragStartY;
        private int dragStartScroll;

        private readonly Font fontName = new("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        private readonly Font fontDate = new("Microsoft YaHei UI", 9F);
        private readonly Font fontDays = new("Microsoft YaHei UI", 11F, FontStyle.Bold);

        public HolidayList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable
                | ControlStyles.UserPaint, true);
            TabStop = false;
            BackColor = UiTheme.PageBg;
            this.AddListener();
            // 滚轮消息发给焦点控件，悬停即持焦保证列表响应滚轮
            MouseEnter += (s, e) => Focus();
        }

        /// <summary>替换列表内容（已按日期升序），days 为距今天数</summary>
        public void SetItems(IReadOnlyList<(HolidayInfo Holiday, int Days)> entries)
        {
            items.Clear();
            foreach (var (holiday, days) in entries)
            {
                items.Add(new Item(holiday.DisplayName, HolidayHero.FormatDate(holiday), days == 0 ? I18n.T("就是今天", "Today") : I18n.F("还有 {0} 天", "{0} days to go", days)));
            }
            ClampScroll();
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

        private int ContentHeight => items.Count * (ItemHeight + ItemGap) - ItemGap;

        private int MaxScroll => Math.Max(0, ContentHeight - ClientSize.Height);

        private void ClampScroll() => scrollTop = Math.Clamp(scrollTop, 0, MaxScroll);

        private Rectangle ThumbRect
        {
            get
            {
                int viewport = ClientSize.Height;
                int content = ContentHeight;
                if (content <= viewport || viewport <= 0)
                {
                    return Rectangle.Empty;
                }

                int thumbHeight = Math.Max(36, viewport * viewport / content);
                int y = MaxScroll == 0 ? 0 : (int)Math.Round((double)scrollTop / MaxScroll * (viewport - thumbHeight));
                return new Rectangle(ClientSize.Width - BarGutter, y, BarWidth, thumbHeight);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int width = ClientSize.Width;
            int daysWidth = 120;

            for (int i = 0; i < items.Count; i++)
            {
                int y = i * (ItemHeight + ItemGap) - scrollTop;
                if (y + ItemHeight < 0 || y > ClientSize.Height)
                {
                    continue;
                }

                var rect = new Rectangle(0, y, width, ItemHeight);
                using (var path = RoundedRect(rect, 10))
                {
                    using var fill = new SolidBrush(UiTheme.CardBg);
                    g.FillPath(fill, path);
                    using var pen = new Pen(UiTheme.CardBorder, 1f);
                    g.DrawPath(pen, path);
                }

                var item = items[i];
                TextRenderer.DrawText(g, item.Name, fontName, new Point(SidePad, y + 11), UiTheme.TextMain);
                TextRenderer.DrawText(g, item.DateText, fontDate, new Point(SidePad, y + 35), UiTheme.TextSub);
                var daysRect = new Rectangle(width - daysWidth - 16, y, daysWidth, ItemHeight);
                TextRenderer.DrawText(g, item.DaysText, fontDays, daysRect, UiTheme.TextSub,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // 细条滚动条（可滚动时才出现）
            var thumb = ThumbRect;
            if (!thumb.IsEmpty)
            {
                using var path = RoundedRect(thumb, BarWidth / 2);
                using var brush = new SolidBrush(UiTheme.RingTrack);
                g.FillPath(brush, path);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scrollTop = Math.Clamp(scrollTop - Math.Sign(e.Delta) * WheelStep, 0, MaxScroll);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var thumb = ThumbRect;
            if (!thumb.IsEmpty && e.X >= thumb.X - BarGutter)
            {
                draggingBar = true;
                dragStartY = e.Y;
                dragStartScroll = scrollTop;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!draggingBar)
            {
                return;
            }

            var thumb = ThumbRect;
            int trackTravel = ClientSize.Height - thumb.Height;
            if (trackTravel > 0)
            {
                int dy = e.Y - dragStartY;
                scrollTop = Math.Clamp(dragStartScroll + (int)Math.Round((double)dy / trackTravel * MaxScroll), 0, MaxScroll);
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            draggingBar = false;
            Capture = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampScroll();
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fontName.Dispose();
                fontDate.Dispose();
                fontDays.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
