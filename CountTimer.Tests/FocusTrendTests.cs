using System.Drawing;
using System.Reflection;
using CountTimer.Model;
using CountTimer.Service;
using CountTimer.View;

namespace CountTimer.Tests;

/// <summary>番茄钟页的近 7 天趋势：星期标注不被裁切、悬停时统计行不挪动、按天统计不漏天</summary>
public class FocusTrendTests
{
    // 趋势小图的基线在第 15 行，基线以下是星期标注
    private const int FirstWeekdayRow = 16;

    [Fact]
    public void WeekdayLabelsAreNotClippedByTheTrendBottomEdge()
    {
        // 8 号字的星期一行要 14~16 像素；标注区给小了，字会被控件底边裁掉，看上去像乱码
        RunOnSta(() =>
        {
            using var trend = new FocusTrendControl();
            trend.SetCounts([1, 2, 3, 4, 5, 6, 7]);
            using Bitmap bitmap = Paint(trend);

            List<int> inkRows = RowsWithInk(bitmap, trend.BackColor, FirstWeekdayRow);
            Assert.NotEmpty(inkRows);
            Assert.True(inkRows.Max() < trend.Height - 1, $"星期标注顶到了控件底边（墨迹到第 {inkRows.Max()} 行，控件高 {trend.Height}）");
        });
    }

    [Fact]
    public void HoveringADayLeavesTheStatsRowInPlace()
    {
        // 统计行整体居中：悬停文案变短时如果照着新文字重新居中，小图会从鼠标底下跳走，
        // 高亮的就不是鼠标指的那一天，鼠标靠左时还会来回闪
        RunOnSta(() =>
        {
            using var control = new PomodoroControl((_, _) => { }, new AppSettings());
            control.Size = new Size(668, 643);
            control.PerformLayout();

            FocusTrendControl trend = control.Controls.OfType<FocusTrendControl>().Single();
            Rectangle rowBefore = trend.Bounds;
            Dictionary<Control, string> textsBefore = control.Controls.Cast<Control>().ToDictionary(child => child, child => child.Text);
            // 确认布局确实算过：统计行离开了原点、落在左半边（整行居中，小图在中线左侧）、排在圆环和按钮下面。
            // 不卡具体横坐标：统计文字里有数字，宽度随测试库里的记录数变
            Assert.InRange(trend.Left, 1, control.Width / 2);
            Assert.True(trend.Top > 200, $"统计行还没排进主区域：{trend.Bounds}");

            trend.SetHover(2);
            Assert.Equal(2, trend.HoverIndex);
            Assert.Equal(rowBefore, trend.Bounds);
            Control[] changed = control.Controls.Cast<Control>().Where(child => child.Text != textsBefore[child]).ToArray();
            Assert.Single(changed);

            trend.SetHover(-1);
            Assert.Equal(rowBefore, trend.Bounds);
            Assert.All(control.Controls.Cast<Control>(), child => Assert.Equal(textsBefore[child], child.Text));
        });
    }

    [Fact]
    public void DailyCountsCoverEveryDayIncludingThoseWithoutSessions()
    {
        // 趋势小图按天取数：最近 7 天每一天都要在结果里，没记录的是 0，柱子才和星期对得上
        var service = new FocusSessionServiceImpl();
        Dictionary<string, int> before = service.GetDailyCounts(FocusTrendControl.Days);
        string date = DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd");
        // AddFocusSession 返回的是影响行数而不是自增主键，收尾时按这个唯一标记删，绝不会碰到别的记录
        string marker = $"test {Guid.NewGuid():N}";

        try
        {
            for (int i = 0; i < 2; i++)
            {
                service.AddFocusSession(new FocusSession
                {
                    sessionDate = date,
                    endTime = marker,
                    focusMinutes = 25
                });
            }

            Dictionary<string, int> after = service.GetDailyCounts(FocusTrendControl.Days);
            Assert.Equal(FocusTrendControl.Days, after.Count);
            for (int offset = -(FocusTrendControl.Days - 1); offset <= 0; offset++)
            {
                string day = DateTime.Today.AddDays(offset).ToString("yyyy-MM-dd");
                Assert.Equal(before[day] + (day == date ? 2 : 0), after[day]);
            }
        }
        finally
        {
            // 测试库跨运行保留：造的记录要自己清掉，否则越攒越多，别的用例看到的统计数字跟着变
            service.Db.Deleteable<FocusSession>().Where(session => session.endTime == marker).ExecuteCommand();
        }
    }

    /// <summary>把控件画到位图上：走的是屏幕上同一套绘制代码</summary>
    private static Bitmap Paint(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(control.BackColor);
        var paint = new PaintEventArgs(g, control.ClientRectangle);
        typeof(Control).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [paint]);
        return bitmap;
    }

    private static List<int> RowsWithInk(Bitmap bitmap, Color background, int fromRow)
    {
        var rows = new List<int>();
        for (int y = fromRow; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (Math.Abs(color.R - background.R) + Math.Abs(color.G - background.G) + Math.Abs(color.B - background.B) > 60)
                {
                    rows.Add(y);
                    break;
                }
            }
        }

        return rows;
    }

    private static void RunOnSta(Action action)
    {
        // WinForms 控件要在单线程单元里创建，xunit 的测试线程默认不是
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
