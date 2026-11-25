using System.Drawing;
using CountTimer.View;

namespace CountTimer.Tests;

/// <summary>托盘剩余时间图标：像素字模与时间格式化</summary>
public class TrayTimeIconTests
{
    [Fact]
    public void EveryGlyphHasExactlyTheDeclaredShape()
    {
        // 字模长度和声明的行列数对不上时，绘制会越界——开始计时的那一刻就会崩
        Assert.Equal(10, TrayTimeIcon.GlyphPatterns.Count);
        Assert.All(TrayTimeIcon.GlyphPatterns, pattern =>
        {
            Assert.Equal(TrayTimeIcon.GlyphColumns * TrayTimeIcon.GlyphRows, pattern.Length);
            Assert.Matches("^[01]+$", pattern);
        });
        Assert.Equal(TrayTimeIcon.GlyphRows, TrayTimeIcon.ColonPattern.Length);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    public void CreatesIconsWithRequestedSizeForEveryCommonScale(int size)
    {
        foreach (string text in new[] { "24:59", "9:05", "1:40", "0:03", "2:40:15" })
        {
            using Icon icon = TrayTimeIcon.Create(text, paused: false, size, lightTaskbar: false);
            using Icon dimmed = TrayTimeIcon.Create(text, paused: true, size, lightTaskbar: true);
            Assert.Equal(size, icon.Width);
            Assert.Equal(size, icon.Height);
            Assert.Equal(size, dimmed.Width);
        }
    }

    [Fact]
    public void IconTextIsVisibleAndNotAllOneColor()
    {
        // 画出来的像素里必须同时有透明背景和不透明文字，否则托盘上什么都看不到
        using Icon icon = TrayTimeIcon.Create("24:59", paused: false, 32, lightTaskbar: false);
        using Bitmap bitmap = icon.ToBitmap();
        var colors = new HashSet<Color>();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                colors.Add(bitmap.GetPixel(x, y));
            }
        }

        Assert.Contains(colors, c => c.A == 0);
        Assert.Contains(colors, c => c.A > 200 && c.R > 200);
    }

    [Theory]
    [InlineData(25 * 60, "25:00")]
    [InlineData(24 * 60 + 59.6, "24:59")]      // 向下取整，不能出现 24:60
    [InlineData(9 * 60 + 5, "09:05")]
    [InlineData(59.2, "00:59")]
    [InlineData(4, "00:04")]
    [InlineData(-3, "00:00")]                   // 已超时按 0 显示
    [InlineData(3600, "1:00")]
    [InlineData(125 * 60 + 7, "2:05")]          // 超过一小时显示 h:mm
    public void FormatsTimeToFitTheTray(double seconds, string expected)
    {
        Assert.Equal(expected, TrayTimeIcon.Format(TimeSpan.FromSeconds(seconds)));
    }
}
