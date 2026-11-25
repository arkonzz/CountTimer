using CountTimer.Model;
using CountTimer.Service;

namespace CountTimer.Tests;

/// <summary>节假日日期换算回归：公历固定、农历（含闰月年）、除夕、清明天文算法、放假安排、自定义日子</summary>
public class HolidayServiceTests
{
    private static readonly DateTime Today = new(2026, 11, 25);

    public static IEnumerable<object[]> KnownDates()
    {
        (string, string)[] known =
        {
            ("春节", "2025-01-29"), ("春节", "2026-02-17"), ("春节", "2027-02-06"), ("春节", "2028-01-26"), ("春节", "2029-02-13"), ("春节", "2030-02-03"),
            ("除夕", "2026-02-16"), ("除夕", "2027-02-05"),
            ("元宵节", "2025-02-12"), ("元宵节", "2026-03-03"),
            ("清明节", "2025-04-04"), ("清明节", "2026-04-05"), ("清明节", "2027-04-05"), ("清明节", "2028-04-04"), ("清明节", "2029-04-04"), ("清明节", "2030-04-05"),
            ("劳动节", "2027-05-01"), ("儿童节", "2027-06-01"), ("国庆节", "2027-10-01"),
            ("端午节", "2025-05-31"), ("端午节", "2026-06-19"), ("端午节", "2027-06-09"), ("端午节", "2028-05-28"),
            ("七夕节", "2025-08-29"), ("七夕节", "2026-08-19"),
            ("中秋节", "2025-10-06"), ("中秋节", "2026-09-25"), ("中秋节", "2027-09-15"), ("中秋节", "2028-10-03"),
            ("重阳节", "2025-10-29"), ("重阳节", "2026-10-18"),
        };

        foreach (var (name, date) in known)
        {
            yield return [name, date];
        }
    }

    [Theory]
    [MemberData(nameof(KnownDates))]
    public void LunarAndSolarFestivalsMatchOfficialDates(string name, string date)
    {
        DateTime expected = DateTime.ParseExact(date, "yyyy-MM-dd", null);

        List<HolidayInfo> holidays = HolidayService.BaseHolidays(2024, 2031);

        HolidayInfo? hit = holidays.FirstOrDefault(h => h.Name == name && h.Date == expected);
        Assert.NotNull(hit);
        Assert.Equal(expected, hit.LastDay);
    }

    [Fact]
    public void UpcomingIsSortedAndContainsOnlyFuture()
    {
        List<HolidayInfo> upcoming = HolidayService.GetUpcoming(Today);

        Assert.Equal(upcoming.OrderBy(h => h.Date), upcoming);
        Assert.All(upcoming, h => Assert.True(h.Date >= Today));
        Assert.Equal("元旦", upcoming[0].Name);
        Assert.Equal(new DateTime(2027, 1, 1), upcoming[0].Date);
    }

    [Fact]
    public void UpcomingCoversMoreThanOneYearWithoutGaps()
    {
        // 2026-11-25 起至少要覆盖到 2028 年春节，且 2028 元旦不能缺（曾因除夕提前落入次年而断档）
        List<HolidayInfo> upcoming = HolidayService.GetUpcoming(Today);

        Assert.Contains(upcoming, h => h.Name == "元旦" && h.Date == new DateTime(2028, 1, 1));
        Assert.Contains(upcoming, h => h.Name == "春节" && h.Date == new DateTime(2028, 1, 26));
    }

    [Fact]
    public void LunarEntriesAreSkippedBeyondCalendarRangeInsteadOfThrowing()
    {
        // 农历历表到 2100 年为止，越界年份不能抛异常，公历固定节日照常生成
        List<HolidayInfo> holidays = HolidayService.BaseHolidays(2098, 2102);

        Assert.Contains(holidays, h => h.Name == "元旦" && h.Date == new DateTime(2102, 1, 1));
        Assert.DoesNotContain(holidays, h => h.Name == "春节" && h.Date.Year > 2100);
        Assert.All(HolidayService.GetUpcoming(new DateTime(2099, 6, 1)), h => Assert.True(h.LastDay >= new DateTime(2099, 6, 1)));
    }

    private static StatutoryYear? Statutory(int year)
        => StatutoryHolidays.Instance.Get(year);

    [Fact]
    public void ArrangementMergesFestivalsIntoOneHolidayEntry()
    {
        var (past, upcoming) = HolidayService.GetAround(Today, statutory: Statutory);

        HolidayInfo spring = Assert.Single(upcoming, h => h.Date == new DateTime(2027, 2, 5));
        Assert.Equal("春节", spring.Name);
        Assert.Equal("Spring Festival", spring.NameEn);
        Assert.Equal(new DateTime(2027, 2, 13), spring.LastDay);
        Assert.Equal("放假 2.5–2.13（9 天） · 补班 1.31、2.14", spring.Note);
        Assert.Equal("Off Feb 5–Feb 13 · 9 days · Work Jan 31, Feb 14", spring.NoteEn);

        // 除夕落在春节假期内，不再单独成条
        Assert.DoesNotContain(upcoming, h => h.Name == "除夕" && h.Date.Year == 2027);
        Assert.Contains(upcoming, h => h.Name == "除夕" && h.Date == new DateTime(2028, 1, 25));   // 2028 安排未公布，仍按节日当天
    }

    [Fact]
    public void ArrangementKeepsSingleDayHolidaysAndAdjacentFestivalsApart()
    {
        var (_, upcoming) = HolidayService.GetAround(Today, statutory: Statutory);

        HolidayInfo dragonBoat = Assert.Single(upcoming, h => h.Name == "端午节" && h.Date.Year == 2027);
        Assert.Equal("放假 1 天", dragonBoat.Note);
        Assert.Equal(new DateTime(2027, 6, 9), dragonBoat.LastDay);

        // 2026 中秋与国庆各放各的
        var (_, upcoming2026) = HolidayService.GetAround(new DateTime(2026, 9, 1), statutory: Statutory);
        Assert.Contains(upcoming2026, h => h.Name == "中秋节" && h.Date == new DateTime(2026, 9, 25) && h.LastDay == new DateTime(2026, 9, 27));
        Assert.Contains(upcoming2026, h => h.Name == "国庆节" && h.Date == new DateTime(2026, 10, 1) && h.LastDay == new DateTime(2026, 10, 7));

        // 非法定节日不受影响
        HolidayInfo lantern = Assert.Single(upcoming, h => h.Name == "元宵节" && h.Date.Year == 2027);
        Assert.Equal("农历正月十五", lantern.Note);
    }

    [Fact]
    public void OngoingHolidayStaysInUpcoming()
    {
        var (past, upcoming) = HolidayService.GetAround(new DateTime(2027, 10, 3), statutory: Statutory);

        HolidayInfo national = Assert.Single(upcoming, h => h.Name == "国庆节" && h.Date.Year == 2027);
        Assert.True(national.IsOngoing(new DateTime(2027, 10, 3)));
        Assert.Equal(national, upcoming[0]);
        Assert.DoesNotContain(past, h => h.Name == "国庆节" && h.Date.Year == 2027);

        // 假期最后一天仍算进行中，次日进入 past
        Assert.True(national.IsOngoing(new DateTime(2027, 10, 7)));
        Assert.False(national.IsOngoing(new DateTime(2027, 10, 8)));
    }

    [Fact]
    public void MergedBlockIsNamedAfterEveryFestivalItCovers()
    {
        // 形如 2025 年：国庆与中秋连成一个假期
        var days = new List<(DateTime, string, bool)>();
        foreach (int day in Enumerable.Range(1, 8))
        {
            days.Add((new DateTime(2025, 10, day), day <= 3 ? "国庆节" : "中秋节", true));
        }

        days.Add((new DateTime(2025, 9, 28), "国庆节", false));
        days.Add((new DateTime(2025, 10, 11), "国庆节", false));
        days.Add((new DateTime(2025, 5, 1), "劳动节", true));
        days.Add((new DateTime(2025, 5, 2), "劳动节", true));
        days.Add((new DateTime(2025, 5, 3), "劳动节", true));
        days.Add((new DateTime(2025, 5, 4), "劳动节", true));
        days.Add((new DateTime(2025, 5, 5), "劳动节", true));
        StatutoryYear data = Assert.IsType<StatutoryYear>(StatutoryYear.Create(2025, days));

        var (past, _) = HolidayService.GetAround(new DateTime(2026, 1, 1), statutory: year => year == 2025 ? data : null);

        HolidayInfo merged = Assert.Single(past, h => h.Name == "国庆节·中秋节");
        Assert.Equal("National Day & Mid-Autumn Festival", merged.NameEn);
        Assert.Equal(new DateTime(2025, 10, 1), merged.Date);
        Assert.Equal(new DateTime(2025, 10, 8), merged.LastDay);
        Assert.Equal("放假 10.1–10.8（8 天） · 补班 9.28、10.11", merged.Note);
    }
}
