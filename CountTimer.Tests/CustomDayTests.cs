using CountTimer.Model;
using CountTimer.Service;

namespace CountTimer.Tests;

/// <summary>自定义生日 / 纪念日：公历与农历的逐年换算、周岁标注</summary>
public class CustomDayTests
{
    private static readonly DateTime Today = new(2026, 11, 25);

    private static CustomDay Day(
        string title = "某人生日", int month = 8, int day = 15, bool lunar = false, int year = 0, int kind = CustomDay.KindBirthday)
        => new() { title = title, month = month, day = day, isLunar = lunar, year = year, kind = kind };

    [Fact]
    public void SolarBirthdayRepeatsYearlyWithAge()
    {
        CustomDay birthday = Day(year: 1996);

        Assert.Equal(new DateTime(2027, 8, 15), HolidayService.NextCustomDay(birthday, Today));

        var (_, upcoming) = HolidayService.GetAround(Today, new[] { birthday });
        HolidayInfo entry = Assert.Single(upcoming, h => h.Name == "某人生日");
        Assert.Equal("生日 · 31 岁", entry.Note);
        Assert.Equal("Birthday · turning 31", entry.NoteEn);
    }

    [Fact]
    public void LunarBirthdayFollowsLunarCalendar()
    {
        CustomDay birthday = Day(lunar: true, day: 1, year: 1990);

        // 2027 年中秋（八月十五）是 9 月 15 日，八月初一即 9 月 1 日
        Assert.Equal(new DateTime(2027, 9, 1), HolidayService.NextCustomDay(birthday, Today));

        var (_, upcoming) = HolidayService.GetAround(Today, new[] { birthday });
        HolidayInfo entry = Assert.Single(upcoming, h => h.Name == "某人生日");
        Assert.Equal("生日 · 农历八月初一 · 37 岁", entry.Note);
        Assert.Equal("Birthday · Lunar 8/1 · turning 37", entry.NoteEn);
    }

    [Fact]
    public void DatesThatDoNotExistEveryYearFallToTheLastDayOfTheMonth()
    {
        // 2027 农历腊月只有廿九（除夕 2028-01-25）
        Assert.Equal(new DateTime(2028, 1, 25), HolidayService.CustomDayDate(Day(lunar: true, month: 12, day: 30), 2027));
        // 平年的 2 月 29 日
        Assert.Equal(new DateTime(2027, 2, 28), HolidayService.CustomDayDate(Day(month: 2, day: 29), 2027));
        Assert.Equal(new DateTime(2028, 2, 29), HolidayService.CustomDayDate(Day(month: 2, day: 29), 2028));
    }

    [Fact]
    public void AnniversaryCountsWholeYears()
    {
        CustomDay anniversary = Day("领证纪念日", month: 5, day: 20, year: 2020, kind: CustomDay.KindAnniversary);

        Assert.Equal(new DateTime(2027, 5, 20), HolidayService.NextCustomDay(anniversary, Today));

        var (_, upcoming) = HolidayService.GetAround(Today, new[] { anniversary });
        HolidayInfo entry = Assert.Single(upcoming, h => h.Name == "领证纪念日");
        Assert.Equal("纪念日 · 7 周年", entry.Note);
        Assert.Equal("Anniversary · 7 yrs", entry.NoteEn);
    }

    [Fact]
    public void StartYearInTheFutureHasNoEarlierOccurrencesAndNoAgeSuffixInFirstYear()
    {
        CustomDay future = Day("项目启动", month: 6, day: 1, year: 2027, kind: CustomDay.KindAnniversary);

        Assert.Equal(new DateTime(2027, 6, 1), HolidayService.NextCustomDay(future, Today));

        var (past, upcoming) = HolidayService.GetAround(Today, new[] { future });
        Assert.DoesNotContain(past, h => h.Name == "项目启动");
        HolidayInfo entry = Assert.Single(upcoming, h => h.Name == "项目启动");
        Assert.Equal("纪念日", entry.Note);
    }

    [Fact]
    public void WithoutStartYearOnlyTheKindIsShown()
    {
        var (_, upcoming) = HolidayService.GetAround(Today, new[] { Day(year: 0) });

        Assert.Equal("生日", Assert.Single(upcoming, h => h.Name == "某人生日").Note);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(13, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 32)]
    public void InvalidMonthOrDayYieldsNoDate(int month, int day)
    {
        Assert.Null(HolidayService.CustomDayDate(Day(month: month, day: day), 2027));
        Assert.Null(HolidayService.NextCustomDay(Day(month: month, day: day), Today));
    }

    [Fact]
    public void CustomDaysSortTogetherWithHolidays()
    {
        var (_, upcoming) = HolidayService.GetAround(
            Today, new[] { Day("妈妈生日", month: 1, day: 20, year: 1967) }, StatutoryHolidays.Instance.Get);

        List<string> order = upcoming.Take(3).Select(h => h.Name).ToList();
        Assert.Equal(new[] { "元旦", "妈妈生日", "春节" }, order);
    }
}
