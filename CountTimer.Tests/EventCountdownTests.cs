using CountTimer.Model;
using CountTimer.Service;

namespace CountTimer.Tests;

/// <summary>事件倒计时并入倒数日列表：每个事件只放下一次、循环事件不上英雄卡、过去的一次性事件不显示</summary>
public class EventCountdownTests
{
    private static readonly DateTime Today = new(2026, 11, 25);

    private static ToDoThing Event(string title, string endTime, RepeatKind kind = RepeatKind.None, int repeatDay = 0)
        => new() { toDoInfo = title, endTime = endTime, isRegular = kind != RepeatKind.None, repeatKind = (int)kind, repeatDay = repeatDay };

    private static StatutoryYear? Statutory(int year) => StatutoryHolidays.Instance.Get(year);

    private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

    [Fact]
    public void OneOffEventShowsOnceWithItsTime()
    {
        var (past, upcoming) = HolidayService.GetAround(Today, events: new[] { Event("交报告", "2026-11-30 09:30:00") });

        HolidayInfo entry = Assert.Single(upcoming, h => h.Name == "交报告");
        Assert.Equal(new DateTime(2026, 11, 30), entry.Date);
        Assert.Equal("09:30", entry.Note);
        Assert.Equal("9:30 AM", entry.NoteEn);
        Assert.True(entry.IsEvent);
        Assert.False(entry.IsRecurringEvent);
        // 事件过去后数据里不留上一次，所以不进 Past
        Assert.DoesNotContain(past, h => h.IsEvent);
    }

    [Fact]
    public void PastOrUnusableOneOffEventsAreNotShown()
    {
        ToDoThing[] events =
        [
            Event("已经过了", "2026-11-24 10:00:00"),
            Event("时间坏了", "不是时间"),
            Event("   ", "2026-12-01 10:00:00"),
        ];

        var (_, upcoming) = HolidayService.GetAround(Today, events: events);

        Assert.DoesNotContain(upcoming, h => h.IsEvent);
        Assert.Null(RepeatRule.NextOccurrence(events[1], DateTime.Now));
    }

    [Fact]
    public void StaleRecurringEventIsRolledForwardToItsNextOccurrence()
    {
        // 后台每 30 秒才把过去的循环事件往前滚一次，这里读到的是还没滚的旧值
        ToDoThing daily = Event("站会", "2020-01-01 09:00:00", RepeatKind.Daily);
        DateTime now = DateTime.Now;

        DateTime next = RepeatRule.NextOccurrence(daily, now)!.Value;
        Assert.Equal(new TimeSpan(9, 0, 0), next.TimeOfDay);
        Assert.InRange(next, now, now.AddDays(1));

        var (_, upcoming) = HolidayService.GetAround(now.Date, events: new[] { daily });
        HolidayInfo entry = Assert.Single(upcoming, h => h.Name == "站会");
        Assert.Equal(next.Date, entry.Date);
        Assert.Equal("09:00 · 每天", entry.Note);
        Assert.Equal("9:00 AM · Daily", entry.NoteEn);
        Assert.True(entry.IsRecurringEvent);
    }

    [Fact]
    public void EachRecurringEventShowsOnlyItsNextOccurrence()
    {
        DateTime now = DateTime.Now;
        ToDoThing daily = Event("站会", Stamp(now.AddDays(1)), RepeatKind.Daily);
        ToDoThing weekly = Event("周会", Stamp(now.AddDays(3)), RepeatKind.Weekly, (int)now.AddDays(3).DayOfWeek);

        var (past, upcoming) = HolidayService.GetAround(now.Date, events: new[] { daily, weekly });

        Assert.Single(upcoming, h => h.Name == "站会");
        Assert.Single(upcoming, h => h.Name == "周会");
        Assert.DoesNotContain(past, h => h.IsEvent);
    }

    [Fact]
    public void WeeklyEventNamesItsWeekday()
    {
        // 十天之后的第一个周三（足够远，什么时候跑都还没到）
        DateTime now = DateTime.Now;
        DateTime wednesday = now.Date.AddDays(10).AddDays(((int)DayOfWeek.Wednesday - (int)now.Date.AddDays(10).DayOfWeek + 7) % 7).AddHours(10);

        var (_, upcoming) = HolidayService.GetAround(now.Date, events: new[] { Event("周会", Stamp(wednesday), RepeatKind.Weekly, (int)DayOfWeek.Wednesday) });

        HolidayInfo entry = Assert.Single(upcoming, h => h.Name == "周会");
        Assert.Equal("10:00 · 每周三", entry.Note);
        Assert.Equal("10:00 AM · Weekly Wednesday", entry.NoteEn);
    }

    [Fact]
    public void HeroSkipsRecurringEventsBecauseTheyAreAlwaysTodayOrTomorrow()
    {
        var standUp = new HolidayInfo { Name = "站会", Date = Today, IsEvent = true, IsRecurringEvent = true };
        var holiday = new HolidayInfo { Name = "元旦", Date = Today.AddDays(3) };

        HolidayInfo? hero = HolidayService.PickHero(new[] { standUp, holiday });

        Assert.Equal("元旦", hero?.Name);
        // 一次性的倒计时可以上英雄卡
        var report = new HolidayInfo { Name = "交报告", Date = Today.AddDays(1), IsEvent = true };
        Assert.Equal("交报告", HolidayService.PickHero(new[] { standUp, report })?.Name);
        Assert.Null(HolidayService.PickHero(new[] { standUp }));
    }

    [Fact]
    public void FestivalComesFirstWhenEverythingFallsOnTheSameDay()
    {
        var payday = new CustomDay { title = "发薪日", kind = CustomDay.KindMonthly, day = 1, adjust = CustomDay.AdjustNone };

        var (_, upcoming) = HolidayService.GetAround(
            new DateTime(2026, 12, 20), new[] { payday }, Statutory, new[] { Event("跨年聚会", "2027-01-01 20:00:00") });

        Assert.Equal(new[] { "元旦", "发薪日", "跨年聚会" }, upcoming.Take(3).Select(h => h.Name).ToList());
        Assert.All(upcoming.Take(3), h => Assert.Equal(new DateTime(2027, 1, 1), h.Date));
    }

    [Theory]
    [InlineData(2, 13, 0, "还有 2 小时 13 分", "2 h 13 min to go")]
    [InlineData(1, 0, 0, "还有 1 小时 0 分", "1 h 0 min to go")]
    [InlineData(0, 13, 20, "还有 13 分", "13 min to go")]
    [InlineData(0, 0, 30, "马上", "any moment")]
    public void TimeLeftTodayIsWordedByScale(int hours, int minutes, int seconds, string zh, string en)
    {
        DateTime now = new DateTime(2026, 11, 26, 15, 46, 41);

        Assert.Equal((zh, en), HolidayService.TimeLeft(now.Add(new TimeSpan(hours, minutes, seconds)), now));
    }

    [Fact]
    public void OnlyEventsLaterTodayGetARemainingTime()
    {
        DateTime now = new DateTime(2026, 11, 26, 15, 0, 0);
        var today = new HolidayInfo { Name = "daily", Date = now.Date, Time = now.AddHours(3), IsEvent = true };
        var tomorrow = new HolidayInfo { Name = "noon", Date = now.Date.AddDays(1), Time = now.Date.AddDays(1).AddHours(12), IsEvent = true };
        var earlierToday = new HolidayInfo { Name = "旧的", Date = now.Date, Time = now.AddHours(-1), IsEvent = true };
        var holiday = new HolidayInfo { Name = "元旦", Date = now.Date };

        Assert.Equal(("还有 3 小时 0 分", "3 h 0 min to go"), HolidayService.TimeLeftToday(today, now));
        Assert.Null(HolidayService.TimeLeftToday(tomorrow, now));
        Assert.Null(HolidayService.TimeLeftToday(earlierToday, now));
        Assert.Null(HolidayService.TimeLeftToday(holiday, now));
    }

    [Theory]
    [InlineData(RepeatKind.None, 0, "", "")]
    [InlineData(RepeatKind.Daily, 0, "每天", "Daily")]
    [InlineData(RepeatKind.Weekdays, 0, "工作日", "Weekdays")]
    [InlineData(RepeatKind.Weekly, 3, "每周三", "Weekly Wednesday")]
    public void RepeatLabelsMatchTheEditor(RepeatKind kind, int weekday, string zh, string en)
    {
        Assert.Equal((zh, en), RepeatRule.Describe(kind, (DayOfWeek)weekday));

        // 事件带重复方式的显示名（ToString）用的也是它
        Assert.Equal(kind == RepeatKind.None ? "周会" : $"周会（{zh}）", Event("周会", Stamp(DateTime.Now), kind, weekday).ToString());
    }
}
