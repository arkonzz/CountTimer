using CountTimer.Service;

namespace CountTimer.Tests;

/// <summary>节假日日期换算回归：公历固定、农历（含闰月年）、除夕、清明天文算法</summary>
public class HolidayServiceTests
{
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
        var holidays = HolidayService.GetUpcoming(new DateTime(expected.Year, 1, 1));

        bool found = holidays.Any(h => h.Name == name && h.Date == expected);
        Assert.True(found, $"{name} {date:yyyy-MM-dd} 未出现在列表中");
    }

    [Fact]
    public void UpcomingIsSortedAndContainsOnlyFuture()
    {
        var today = new DateTime(2026, 11, 25);
        List<Model.HolidayInfo> upcoming = HolidayService.GetUpcoming(today);

        Assert.Equal(upcoming.OrderBy(h => h.Date), upcoming);
        Assert.All(upcoming, h => Assert.True(h.Date >= today));
        Assert.Equal("元旦", upcoming[0].Name);
        Assert.Equal(new DateTime(2027, 1, 1), upcoming[0].Date);
    }

    [Fact]
    public void UpcomingCoversMoreThanOneYearWithoutGaps()
    {
        // 2026-11-25 起至少要覆盖到 2028 年春节，且 2028 元旦不能缺（曾因除夕提前落入次年而断档）
        List<Model.HolidayInfo> upcoming = HolidayService.GetUpcoming(new DateTime(2026, 11, 25));

        Assert.Contains(upcoming, h => h.Name == "元旦" && h.Date == new DateTime(2028, 1, 1));
        Assert.Contains(upcoming, h => h.Name == "春节" && h.Date == new DateTime(2028, 1, 26));
    }
}
