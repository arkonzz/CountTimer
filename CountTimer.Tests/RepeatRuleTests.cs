using CountTimer.Model;
using CountTimer.Service;

namespace CountTimer.Tests;

/// <summary>重复事件的下一次发生时间，以及后台到点提醒的判定</summary>
public class RepeatRuleTests
{
    private static readonly DateTime WednesdayMorning = new(2026, 11, 25, 10, 0, 0);   // 周三

    private static bool PlainWeekday(DateTime date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    [Fact]
    public void DailyUsesTodayWhenTimeIsStillAheadOtherwiseTomorrow()
    {
        Assert.Equal(new DateTime(2026, 11, 25, 11, 0, 0), RepeatRule.Next(RepeatKind.Daily, 0, new TimeSpan(11, 0, 0), WednesdayMorning, PlainWeekday));
        Assert.Equal(new DateTime(2026, 11, 26, 9, 0, 0), RepeatRule.Next(RepeatKind.Daily, 0, new TimeSpan(9, 0, 0), WednesdayMorning, PlainWeekday));
        Assert.Equal(new DateTime(2026, 11, 26, 10, 0, 0), RepeatRule.Next(RepeatKind.Daily, 0, new TimeSpan(10, 0, 0), WednesdayMorning, PlainWeekday));
    }

    [Fact]
    public void WeeklyLandsOnTheGivenWeekday()
    {
        Assert.Equal(new DateTime(2026, 11, 30, 9, 0, 0), RepeatRule.Next(RepeatKind.Weekly, (int)DayOfWeek.Monday, new TimeSpan(9, 0, 0), WednesdayMorning, PlainWeekday));
        // 当天就是周三且时间还没到：今天
        Assert.Equal(new DateTime(2026, 11, 25, 18, 0, 0), RepeatRule.Next(RepeatKind.Weekly, (int)DayOfWeek.Wednesday, new TimeSpan(18, 0, 0), WednesdayMorning, PlainWeekday));
        // 当天时间已过：下周三
        Assert.Equal(new DateTime(2026, 12, 2, 8, 0, 0), RepeatRule.Next(RepeatKind.Weekly, (int)DayOfWeek.Wednesday, new TimeSpan(8, 0, 0), WednesdayMorning, PlainWeekday));
    }

    [Fact]
    public void WeekdaysSkipsWeekendsHolidaysAndCountsMakeUpDays()
    {
        // 2026 年安排：中秋 9.25–9.27、国庆 10.1–10.7 放假；10.10（周六）等补班日
        DateTime Next(DateTime now) => RepeatRule.Next(RepeatKind.Weekdays, 0, new TimeSpan(9, 0, 0), now, StatutoryHolidays.Instance.IsWorkday);

        Assert.Equal(new DateTime(2026, 9, 28, 9, 0, 0), Next(new DateTime(2026, 9, 25, 18, 0, 0)));    // 周五中秋放假，跨过周末到周一
        Assert.Equal(new DateTime(2026, 10, 8, 9, 0, 0), Next(new DateTime(2026, 9, 30, 18, 0, 0)));     // 跨过整个国庆假期
        Assert.Equal(new DateTime(2026, 10, 10, 9, 0, 0), Next(new DateTime(2026, 10, 9, 18, 0, 0)));    // 周六补班算工作日
        Assert.Equal(new DateTime(2026, 10, 12, 9, 0, 0), Next(new DateTime(2026, 10, 10, 10, 0, 0)));   // 周日不算
        Assert.Equal(new DateTime(2028, 3, 6, 9, 0, 0), Next(new DateTime(2028, 3, 3, 18, 0, 0)));       // 2028 安排未公布，按普通周末处理
    }

    [Fact]
    public void FillsInMissingWorkdaysWithPlainWeekdays()
    {
        // 连续多日都不是工作日时不能死循环，40 天后退回按天重复
        DateTime next = RepeatRule.Next(RepeatKind.Weekdays, 0, new TimeSpan(9, 0, 0), WednesdayMorning, _ => false);
        Assert.Equal(new DateTime(2026, 11, 26, 9, 0, 0), next);
    }

    [Fact]
    public void LegacyRowsThatOnlyHaveIsRegularCountAsDaily()
    {
        Assert.Equal(RepeatKind.Daily, new ToDoThing { isRegular = true }.Kind);
        Assert.Equal(RepeatKind.None, new ToDoThing { isRegular = false }.Kind);
        Assert.Equal(RepeatKind.Weekdays, new ToDoThing { isRegular = true, repeatKind = (int)RepeatKind.Weekdays }.Kind);
    }

    [Fact]
    public void BackgroundNotifiesEveryEventOnceItsTimeComes()
    {
        // 事件归倒数日页之后没有"正在跟随的倒计时"了：循环的和一次性的到点都提醒
        ToDoThing regular = new() { Id = 1, isRegular = true, endTime = "2026-11-25 09:00:00" };
        ToDoThing oneOff = new() { Id = 2, endTime = "2026-11-25 09:00:00" };
        ToDoThing dueRightNow = new() { Id = 4, endTime = "2026-11-25 10:00:00" };
        ToDoThing future = new() { Id = 3, endTime = "2026-11-25 11:00:00" };

        Assert.True(ExpiredEventCleanupWorker.ShouldNotify(regular, WednesdayMorning));
        Assert.True(ExpiredEventCleanupWorker.ShouldNotify(oneOff, WednesdayMorning));
        Assert.True(ExpiredEventCleanupWorker.ShouldNotify(dueRightNow, WednesdayMorning));
        Assert.False(ExpiredEventCleanupWorker.ShouldNotify(future, WednesdayMorning));
        Assert.False(ExpiredEventCleanupWorker.ShouldNotify(new ToDoThing { endTime = "不是时间" }, WednesdayMorning));
    }
}
