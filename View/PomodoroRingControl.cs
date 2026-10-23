using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AntdUI;

namespace CountTimer.View
{
    /// <summary>
    /// 番茄钟圆环进度控件：背景环 + 进度弧 + 中央文字，配色取自 AntdUI 色板随主题切换
    /// </summary>
    public class PomodoroRingControl : Control, AntdUI.IEventListener
    {
        private const float RingThicknessRatio = 0.075f;

        private Color _trackColor = UiTheme.RingTrack;
        private Color _textColor = UiTheme.TextMain;
        private Color _subTextColor = UiTheme.TextSub;
        // 页面同色背景：不依赖 WinForms 透明（透明在主题切换局部重绘时会失效渲染成白色）
        private Color _bgColor = UiTheme.PageBg;

        private float _progress = 1f;
        private string _titleText = "专注";
        private string _timeText = "25:00";
        private Color _ringColor = UiTheme.FocusColor;

        private readonly Font _titleFont = new Font("Microsoft YaHei UI", 14f, FontStyle.Regular, GraphicsUnit.Point);

        public PomodoroRingControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint, true);
            this.AddListener();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            // 移动（尺寸不变）时 WinForms 走位图平移不重绘，会产生残影，强制重绘
            Invalidate();
        }

        event EventHandler? AntdUI.IEventListener.Disposed
        {
            add => Disposed += value;
            remove => Disposed -= value;
        }

        int AntdUI.IEventListener.GetHashCode() => base.GetHashCode();

        public void HandleEvent(AntdUI.EventType id, object? tag)
        {
            if (id == AntdUI.EventType.THEME)
            {
                SetTheme();
            }
        }

        /// <summary>剩余比例 0~1，1 表示满</summary>
        public float Progress
        {
            get => _progress;
            set
            {
                float clamped = Math.Clamp(value, 0f, 1f);
                if (Math.Abs(clamped - _progress) < 0.0005f)
                {
                    return;
                }

                _progress = clamped;
                Invalidate();
            }
        }

        public string TitleText
        {
            get => _titleText;
            set
            {
                if (_titleText == value)
                {
                    return;
                }

                _titleText = value ?? string.Empty;
                Invalidate();
            }
        }

        public string TimeText
        {
            get => _timeText;
            set
            {
                if (_timeText == value)
                {
                    return;
                }

                _timeText = value ?? string.Empty;
                Invalidate();
            }
        }

        public Color RingColor
        {
            get => _ringColor;
            set
            {
                _ringColor = value;
                Invalidate();
            }
        }

        /// <summary>按当前 AntdUI 主题刷新配色</summary>
        public void SetTheme()
        {
            _bgColor = UiTheme.PageBg;
            _trackColor = UiTheme.RingTrack;
            _textColor = UiTheme.TextMain;
            _subTextColor = UiTheme.TextSub;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 用页面同色填充背景（外扩1px防止边缘漏缝），避免透明失效渲染成白色
            using (var bgBrush = new SolidBrush(_bgColor))
            {
                g.FillRectangle(bgBrush, -1, -1, Width + 2, Height + 2);
            }

            // 圆环区域（正方形，居中）：描边以矩形为中心线、向内外各扩 thickness/2，
            // 矩形必须按 笔宽/2 + 抗锯齿余量 收缩，否则描边外缘超出控件边界，
            // 会在上下左右四个极点被裁成平边（弧线不完整）
            int size = Math.Min(Width, Height);
            float thickness = Math.Max(10f, (size - 12) * RingThicknessRatio);
            float penOverhang = thickness / 2f + 1f;
            float ringSize = size - penOverhang * 2f;
            RectangleF ringRect = new RectangleF(
                (Width - ringSize) / 2f,
                (Height - ringSize) / 2f,
                ringSize,
                ringSize);

            // 背景环（整圆必须用 DrawEllipse：DrawArc 画 360° 会在贝塞尔分段接缝处随机丢段）
            using (Pen trackPen = new Pen(_trackColor, thickness))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawEllipse(trackPen, ringRect);
            }

            // 进度弧：从顶部（-90°）起，随剩余时间缩短；满圆时同样用 DrawEllipse 避免接缝丢段
            if (_progress >= 0.999f)
            {
                using Pen progressPen = new Pen(_ringColor, thickness);
                progressPen.StartCap = LineCap.Round;
                progressPen.EndCap = LineCap.Round;
                g.DrawEllipse(progressPen, ringRect);
            }
            else if (_progress > 0f)
            {
                using Pen progressPen = new Pen(_ringColor, thickness);
                progressPen.StartCap = LineCap.Round;
                progressPen.EndCap = LineCap.Round;
                float sweep = 360f * _progress;
                g.DrawArc(progressPen, ringRect, -90f, sweep);
            }

            // 中央文字（时间字号随环尺寸缩放）
            float timeSize = Math.Clamp(ringSize / 9f, 16f, 34f);
            using (StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoClip
            })
            using (var timeFont = new Font(_titleFont.Name, timeSize, FontStyle.Bold, GraphicsUnit.Point))
            using (var titleBrush = new SolidBrush(_subTextColor))
            using (var timeBrush = new SolidBrush(_textColor))
            {
                PointF center = new PointF(Width / 2f, Height / 2f);
                g.DrawString(_titleText, _titleFont, titleBrush, center.X, center.Y - ringSize * 0.17f, format);
                g.DrawString(_timeText, timeFont, timeBrush, center.X, center.Y + ringSize * 0.03f, format);
            }
        }
    }
}
