using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AntdUI;

namespace CountTimer.View
{
    /// <summary>
    /// 设计令牌薄封装：全部读取 AntdUI 的 Colour 令牌（跟随 Config.Mode 明暗切换），
    /// 仅番茄钟三阶段色为自有品牌色
    /// </summary>
    public static class UiTheme
    {
        /// <summary>番茄钟三阶段主色</summary>
        public static readonly Color FocusColor = Color.FromArgb(228, 88, 53);
        public static readonly Color BreakColor = Color.FromArgb(103, 194, 58);
        public static readonly Color LongBreakColor = Color.FromArgb(64, 140, 255);

        /// <summary>页面背景（窗体/Tabs 页同色）</summary>
        public static Color PageBg => AntdUI.Colour.BgLayout.Get();
        /// <summary>卡片背景（浮层色）</summary>
        public static Color CardBg => AntdUI.Colour.BgElevated.Get();
        /// <summary>卡片描边</summary>
        public static Color CardBorder => AntdUI.Colour.BorderSecondary.Get();
        /// <summary>主文字</summary>
        public static Color TextMain => AntdUI.Colour.Text.Get();
        /// <summary>次级文字</summary>
        public static Color TextSub => AntdUI.Colour.TextSecondary.Get();
        /// <summary>圆环底环（已流逝部分）——用 Fill 保证明暗主题下都清晰可见，
        /// FillTertiary 在亮色下只有 4% 透明黑，几乎看不见</summary>
        public static Color RingTrack => AntdUI.Colour.Fill.Get();
    }

    /// <summary>
    /// 圆角卡片容器：背景/描边取自 AntdUI 色板，实现 IEventListener 随主题自动重绘
    /// </summary>
    public class CardPanel : System.Windows.Forms.Panel, AntdUI.IEventListener
    {
        private const int Radius = 10;

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint, true);
            BackColor = UiTheme.CardBg;
            this.AddListener();
        }

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                BackColor = UiTheme.CardBg;
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Rectangle rect = ClientRectangle;
            if (rect.Width < Radius * 2 + 2 || rect.Height < Radius * 2 + 2)
            {
                return;
            }

            Region?.Dispose();
            Region = new Region(BuildPath(rect));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = BuildPath(ClientRectangle);
            using var pen = new Pen(UiTheme.CardBorder, 1f);
            e.Graphics.DrawPath(pen, path);
        }

        private GraphicsPath BuildPath(Rectangle rect)
        {
            rect.Width -= 1;
            rect.Height -= 1;
            var path = new GraphicsPath();
            int d = Radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
