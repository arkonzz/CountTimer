using CountTimer.Model;
using CountTimer.Service;

namespace CountTimer.Tests;

/// <summary>每月重复的日子（发薪日、还款日这类）：月末兜底、遇休息日提前 / 顺延、只占列表一两项</summary>
public class MonthlyDayTests
{
    private static readonly DateTime Today = new(2026, 11, 25);

    private static CustomDay Monthly(int day = 10, int adjust = CustomDay.AdjustEarlier, string title = "发薪日")
        => new() { title = title, kind = CustomDay.KindMonthly, day = day, adjust = adjust };

    private static StatutoryYear? Statutory(int year) => StatutoryHolidays.Instance.Get(year);

    private static DateTime Date(int year, int month, int day) => new(year, month, day);

    [Theory]
    [InlineData(10, CustomDay.AdjustEarlier, 2026, 12, "2026-12-10")]    // 周四，不用挪
    [InlineData(6, CustomDay.AdjustEarlier, 2026, 12, "2026-12-04")]     // 周日 → 上一个工作日是周五
    [InlineData(6, CustomDay.AdjustLater, 2026, 12, "2026-12-07")]       // 周日 → 下周一
    [InlineData(6, CustomDay.AdjustNone, 2026, 12, "2026-12-06")]
    [InlineData(31, CustomDay.AdjustEarlier, 2026, 11, "2026-11-30")]    // 11 月没有 31 号 → 月末
    [InlineData(30, CustomDay.AdjustEarlier, 2027, 2, "2027-02-26")]     // 2 月没有 30 号 → 28 日是周日 → 周五
    [InlineData(1, CustomDay.AdjustEarlier, 2027, 1, "2026-12-31")]      // 元旦放假 → 提前到前一天，跨进了上个月
    [InlineData(5, CustomDay.AdjustEarlier, 2027, 2, "2027-02-04")]      // 春节 2.5 开始放 → 提前到 2.4
    [InlineData(5, CustomDay.AdjustLater, 2027, 2, "2027-02-14")]        // 一直放到 2.13，2.14 周日补班，顺延停在补班日
    [InlineData(1, CustomDay.AdjustEarlier, 2027, 10, "2027-09-30")]     // 国庆 → 提前到 9.30
    [InlineData(31, CustomDay.AdjustEarlier, 2027, 1, "2027-01-31")]     // 1.31 周日补班，算工作日，不用挪
    public void AdjustsToTheNearestWorkdayAndFallsBackToMonthEnd(int day, int adjust, int year, int month, string expected)
    {
        DateTime? date = HolidayService.MonthlyDayDate(Monthly(day, adjust), year, month, d => StatutoryYear.IsWorkday(d, Statutory(d.Year)));

        Assert.Equal(DateTime.ParseExact(expected, "yyyy-MM-dd", null), date);
    }

    [Fact]
    public void WithoutAnArrangementOnlyWeekendsAreDaysOff()
    {
        // 2028 年的安排还没公布：1 月 1 日按普通周六算，提前到周五
        DateTime? date = HolidayService.MonthlyDayDate(Monthly(1), 2028, 1, d => StatutoryYear.IsWorkday(d, null));

        Assert.Equal(Date(2027, 12, 31), date);
    }

    [Fact]
    public void NextOccurrenceIsTheSoonestOne()
    {
        Assert.Equal(Date(2026, 12, 10), HolidayService.NextCustomDay(Monthly(10), Today, Statutory));
        // 从 2026-12-15 看：名义上的下一月是 1 月 1 日，放假提前后落在 12.31
        Assert.Equal(Date(2026, 12, 31), HolidayService.NextCustomDay(Monthly(1), Date(2026, 12, 15), Statutory));
        // 当天（含）起算
        Assert.Equal(Date(2026, 12, 10), HolidayService.NextCustomDay(Monthly(10), new DateTime(2026, 12, 10, 9, 30, 0), Statutory));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void InvalidDayYieldsNoDate(int day)
    {
        Assert.Null(HolidayService.MonthlyDayDate(Monthly(day), 2026, 12));
        Assert.Null(HolidayService.NextCustomDay(Monthly(day), Today, Statutory));

        var (past, upcoming) = HolidayService.GetAround(Today, new[] { Monthly(day) }, Statutory);
        Assert.DoesNotContain(past, h => h.Name == "发薪日");
        Assert.DoesNotContain(upcoming, h => h.Name == "发薪日");
    }

    [Fact]
    public void OnlyTheLastAndNextOccurrencesJoinTheHolidayList()
    {
        var (past, upcoming) = HolidayService.GetAround(Today, new[] { Monthly(10) }, Statutory);

        Assert.Equal(Date(2026, 11, 10), Assert.Single(past, h => h.Name == "发薪日").Date);
        HolidayInfo next = Assert.Single(upcoming, h => h.Name == "发薪日");
        Assert.Equal(Date(2026, 12, 10), next.Date);
        Assert.Equal("每月10日 · 遇休息日提前", next.Note);
        Assert.Equal("10th monthly · earlier if off", next.NoteEn);
        Assert.True(next.IsMonthly);
        // 每月一次，但不能把节日挤出固定长度的列表
        Assert.Contains(upcoming, h => h.Name == "元旦" && h.Date == Date(2028, 1, 1));
    }

    [Fact]
    public void ShiftedDateExplainsItself()
    {
        var (_, upcoming) = HolidayService.GetAround(Date(2026, 12, 20), new[] { Monthly(1) }, Statutory);

        HolidayInfo next = Assert.Single(upcoming, h => h.Name == "发薪日");
        Assert.Equal(Date(2026, 12, 31), next.Date);
        Assert.Equal("每月1日 · 遇休息日提前（原1月1日）", next.Note);
        Assert.Equal("1st monthly · earlier if off (was Jan 1)", next.NoteEn);
    }

    [Fact]
    public void FestivalComesFirstWhenItFallsOnTheSameDay()
    {
        // 不调整的发薪日和元旦同一天：英雄卡显示的仍是节日
        var (_, upcoming) = HolidayService.GetAround(Date(2026, 12, 20), new[] { Monthly(1, CustomDay.AdjustNone) }, Statutory);

        Assert.Equal(new[] { "元旦", "发薪日" }, upcoming.Take(2).Select(h => h.Name).ToList());
        Assert.All(upcoming.Take(2), h => Assert.Equal(Date(2027, 1, 1), h.Date));
    }

    [Fact]
    public void MonthlyDayCanBeTheNextDateToShow()
    {
        var (past, upcoming) = HolidayService.GetAround(Date(2026, 12, 1), new[] { Monthly(10) }, Statutory);

        Assert.Equal("发薪日", upcoming[0].Name);
        Assert.Equal(Date(2026, 12, 10), upcoming[0].Date);
        // 上一个就是它自己的上一次
        Assert.Equal(Date(2026, 11, 10), past[^1].Date);
    }

    [Theory]
    [InlineData(CustomDay.AdjustEarlier, 15, "每月15日 · 遇休息日提前", "15th monthly · earlier if off")]
    [InlineData(CustomDay.AdjustLater, 22, "每月22日 · 遇休息日顺延", "22nd monthly · later if off")]
    [InlineData(CustomDay.AdjustNone, 3, "每月3日", "3rd monthly")]
    public void RuleTextMentionsTheAdjustment(int adjust, int day, string zh, string en)
    {
        Assert.Equal((zh, en), HolidayService.MonthlyRule(Monthly(day, adjust)));
    }
}
