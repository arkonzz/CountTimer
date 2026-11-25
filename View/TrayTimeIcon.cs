using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CountTimer.View
{
    /// <summary>
    /// 托盘剩余时间图标。普通字体在 16px 里放不下 "24:59" 这么多位数字，
    /// 所以用 3×7 的像素数字，按托盘图标尺寸等比放大；文字颜色跟随任务栏明暗。
    /// Create 返回的 Icon 由调用方负责释放。
    /// </summary>
    internal static class TrayTimeIcon
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        private const string Digits = "0123456789";

        // 每个数字 3 列 × 7 行，"1" 为点亮
        internal const int GlyphColumns = 3;
        internal const int GlyphRows = 7;
        internal static IReadOnlyList<string> GlyphPatterns { get; } =
        [
            "111101101101101101111", // 0
            "010110010010010010111", // 1
            "111001001111100100111", // 2
            "111001001111001001111", // 3
            "101101101111001001001", // 4
            "111100100111001001111", // 5
            "111100100111101101111", // 6
            "111001001010010010010", // 7
            "111101101111101101111", // 8
            "111101101111001001111", // 9
        ];
        internal const string ColonPattern = "0010010";
        private const string Colon = ColonPattern;
        private const int Step = 1;

        /// <summary>托盘图标在当前缩放下的像素尺寸</summary>
        public static int TraySize => System.Windows.Forms.SystemInformation.SmallIconSize.Width;

        /// <summary>任务栏是否为浅色（系统主题），读不到时按 Windows 默认的深色处理</summary>
        public static bool IsTaskbarLight()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int light && light == 1;
        }

        /// <summary>把剩余时间格式化成托盘里放得下的形式：不足 1 小时为 mm:ss，否则为 h:mm</summary>
        public static string Format(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            // 59.6 秒这类要向下取整，避免出现 00:60
            int totalSeconds = (int)remaining.TotalSeconds;
            return totalSeconds >= 3600
                ? $"{totalSeconds / 3600}:{totalSeconds % 3600 / 60:D2}"
                : $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
        }

        public static Icon Create(string text, bool paused, int size, bool lightTaskbar)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                Draw(g, text, size, paused, lightTaskbar);
            }

            IntPtr handle = bitmap.GetHicon();
            try
            {
                // FromHandle 不拥有句柄；Clone 出一份自带句柄的副本后再归还临时句柄
                using var temporary = Icon.FromHandle(handle);
                return (Icon)temporary.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        private static void Draw(Graphics g, string text, int size, bool paused, bool lightTaskbar)
        {
            // 图标尺寸以 16px 为基准：整数倍时像素严格对齐，非整数倍时逐格四舍五入
            float scale = size / 16f;
            int units = 0;
            foreach (char character in text)
            {
                if (character == ':' || Digits.Contains(character))
                {
                    units += ColumnsOf(character) + Step;
                }
            }

            units = Math.Max(0, units - Step);        // 最后一个字符后面不留间隔
            int width = (int)Math.Round(units * scale);
            int height = (int)Math.Round(GlyphRows * scale);
            int originX = (size - width) / 2;
            int originY = (size - height) / 2;

            int alpha = paused ? 140 : 255;
            using var brush = new SolidBrush(lightTaskbar
                ? Color.FromArgb(alpha, 32, 32, 32)
                : Color.FromArgb(alpha, 255, 255, 255));

            int column = 0;
            foreach (char character in text)
            {
                if (character != ':' && !Digits.Contains(character))
                {
                    continue;
                }

                int columns = ColumnsOf(character);
                string pattern = character == ':' ? Colon : GlyphPatterns[Digits.IndexOf(character)];
                for (int row = 0; row < GlyphRows; row++)
                {
                    for (int col = 0; col < columns; col++)
                    {
                        if (pattern[row * columns + col] != '1')
                        {
                            continue;
                        }

                        int left = Pixel(column + col, scale);
                        int top = Pixel(row, scale);
                        g.FillRectangle(brush,
                            originX + left,
                            originY + top,
                            Pixel(column + col + 1, scale) - left,
                            Pixel(row + 1, scale) - top);
                    }
                }

                column += columns + Step;
            }
        }

        private static int ColumnsOf(char character) => character == ':' ? 1 : GlyphColumns;

        private static int Pixel(int unit, float scale) => (int)Math.Round(unit * scale);
    }
}
