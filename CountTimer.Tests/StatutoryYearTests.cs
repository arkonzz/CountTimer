using System.IO;
using System.Reflection;
using System.Text;
using CountTimer.Service;

namespace CountTimer.Tests;

/// <summary>法定节假日安排数据的解析、校验与分段</summary>
public class StatutoryYearTests
{
    private const string Upstream2027 = """
        {
            "$schema": "https://example.invalid/schema.json",
            "year": 2027,
            "papers": ["https://example.invalid/paper.htm"],
            "days": [
                { "name": "元旦", "date": "2027-01-01", "isOffDay": true },
                { "name": "元旦", "date": "2027-01-02", "isOffDay": true },
                { "name": "春节", "date": "2027-01-31", "isOffDay": false },
                { "name": "春节", "date": "2027-02-05", "isOffDay": true },
                { "name": "春节", "date": "2027-02-06", "isOffDay": true },
                { "name": "国庆节", "date": "2027-10-01", "isOffDay": true },
                { "name": "中秋节", "date": "2027-10-02", "isOffDay": true },
                { "name": "国庆节", "date": "2027-10-03", "isOffDay": true },
                { "name": "劳动节", "date": "2027-05-01", "isOffDay": true },
                { "name": "劳动节", "date": "2027-05-02", "isOffDay": true },
                { "name": "清明节", "date": "2027-04-05", "isOffDay": true },
                { "name": "清明节", "date": "2027-04-06", "isOffDay": true },
                { "name": "端午节", "date": "2027-06-09", "isOffDay": true },
                { "name": "重阳节", "date": "2027-10-14", "isOffDay": true }
            ]
        }
        """;

    [Fact]
    public void ParsesUpstreamFormatIntoBlocksAndMakeUpDays()
    {
        StatutoryYear? data = StatutoryYear.Parse(Upstream2027, 2027);

        Assert.NotNull(data);
        Assert.Equal(
            new[] { ("元旦", "2027-01-01", "2027-01-02"), ("春节", "2027-02-05", "2027-02-06"), ("清明节", "2027-04-05", "2027-04-06"), ("劳动节", "2027-05-01", "2027-05-02"), ("端午节", "2027-06-09", "2027-06-09"), ("国庆节·中秋节", "2027-10-01", "2027-10-03"), ("重阳节", "2027-10-14", "2027-10-14") },
            data.OffBlocks.Select(b => (string.Join("·", b.Names), b.Start.ToString("yyyy-MM-dd"), b.End.ToString("yyyy-MM-dd"))));
        Assert.Equal(new DateTime(2027, 1, 31), Assert.Single(data.MakeUpWorkdays));
        Assert.True(data.IsOffDay(new DateTime(2027, 10, 3)));
        Assert.False(data.IsOffDay(new DateTime(2027, 10, 4)));
        Assert.False(data.IsOffDay(new DateTime(2026, 1, 1)));
    }

    [Fact]
    public void ParsesPlainArrayCacheFormat()
    {
        const string cache = """[{"name":"元旦","date":"2027-01-01","isOffDay":true},{"name":"春节","date":"2027-02-05","isOffDay":true},{"name":"春节","date":"2027-02-06","isOffDay":true},{"name":"春节","date":"2027-02-07","isOffDay":true},{"name":"春节","date":"2027-02-08","isOffDay":true},{"name":"春节","date":"2027-02-09","isOffDay":true},{"name":"春节","date":"2027-02-10","isOffDay":true},{"name":"春节","date":"2027-02-11","isOffDay":true},{"name":"春节","date":"2027-02-12","isOffDay":true},{"name":"春节","date":"2027-02-13","isOffDay":true},{"name":"春节","date":"2027-01-31","isOffDay":false}]""";

        StatutoryYear? data = StatutoryYear.Parse(cache, 2027);

        Assert.NotNull(data);
        Assert.Equal(2, data.OffBlocks.Count);
        HolidayBlock spring = data.OffBlocks[1];
        Assert.Equal(new DateTime(2027, 2, 5), spring.Start);
        Assert.Equal(new DateTime(2027, 2, 13), spring.End);
    }

    [Fact]
    public void RejectsMalformedOrInconsistentData()
    {
        // 十条合法数据打底，每个样本只带一个缺陷，确保拒绝的原因就是那个缺陷
        const string ten = """
            [{"name":"春节","date":"2027-02-01","isOffDay":true},{"name":"春节","date":"2027-02-02","isOffDay":true},{"name":"春节","date":"2027-02-03","isOffDay":true},{"name":"春节","date":"2027-02-04","isOffDay":true},{"name":"春节","date":"2027-02-05","isOffDay":true},{"name":"春节","date":"2027-02-06","isOffDay":true},{"name":"春节","date":"2027-02-07","isOffDay":true},{"name":"春节","date":"2027-02-08","isOffDay":true},{"name":"春节","date":"2027-02-09","isOffDay":true},{"name":"春节","date":"2027-02-10","isOffDay":true}]
            """;
        string open = ten[..^1];        // 去掉结尾的 ]，方便再追加一条

        string[] bad =
        {
            "not json",
            """{"year":2027,"days":[]}""",
            """{"year":2027}""",
            ten.Replace("2027-02-01", "2027-02-02"),                                   // 日期重复
            open + """{"name":"元旦","date":"2026-01-01","isOffDay":true}]""",          // 日期不在目标年份
            open + """{"name":"元旦","date":"2027-03-01"}]""",                          // 缺 isOffDay
            """[{"name":"元旦","date":"2027/01/01","isOffDay":true},""" + ten[1..],      // 日期格式不对
            """{"year":2026,"days":""" + ten + "}",                                     // 声明的年份不符
            ten.Replace("\"isOffDay\":true", "\"isOffDay\":\"yes\""),                    // isOffDay 不是布尔
        };

        Assert.All(bad, json => Assert.Null(StatutoryYear.Parse(json, 2027)));
    }

    [Fact]
    public void RejectsDataWithoutAnyOffDay()
    {
        var days = Enumerable.Range(1, 12)
            .Select(day => (new DateTime(2027, 1, day), "元旦", day == 1))
            .ToList();
        days.RemoveAll(d => d.Item3);

        Assert.Null(StatutoryYear.Create(2027, days));
    }

    [Theory]
    [InlineData(2026)]
    [InlineData(2027)]
    public void BundledDataParsesAndMatchesStateCouncilNotices(int year)
    {
        Assembly assembly = typeof(StatutoryYear).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Resources.Holidays.{year}.json");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        StatutoryYear? data = StatutoryYear.Parse(reader.ReadToEnd(), year);

        Assert.NotNull(data);
        Dictionary<string, HolidayBlock> blocks = data.OffBlocks.ToDictionary(b => string.Join("·", b.Names), b => b);
        Assert.Equal(7, blocks.Count);

        // 国务院办公厅通知原文中的放假区间与补班日
        if (year == 2027)
        {
            Assert.Equal(("2027-01-01", "2027-01-03"), Range(blocks["元旦"]));
            Assert.Equal(("2027-02-05", "2027-02-13"), Range(blocks["春节"]));
            Assert.Equal(("2027-04-03", "2027-04-05"), Range(blocks["清明节"]));
            Assert.Equal(("2027-05-01", "2027-05-05"), Range(blocks["劳动节"]));
            Assert.Equal(("2027-06-09", "2027-06-09"), Range(blocks["端午节"]));
            Assert.Equal(("2027-09-15", "2027-09-15"), Range(blocks["中秋节"]));
            Assert.Equal(("2027-10-01", "2027-10-07"), Range(blocks["国庆节"]));
            Assert.Equal("2027-01-31,2027-02-14,2027-05-08,2027-09-26,2027-10-09", string.Join(',', data.MakeUpWorkdays.Select(d => d.ToString("yyyy-MM-dd"))));
        }
        else
        {
            Assert.Equal(("2026-01-01", "2026-01-03"), Range(blocks["元旦"]));
            Assert.Equal(("2026-02-15", "2026-02-23"), Range(blocks["春节"]));
            Assert.Equal(("2026-04-04", "2026-04-06"), Range(blocks["清明节"]));
            Assert.Equal(("2026-05-01", "2026-05-05"), Range(blocks["劳动节"]));
            Assert.Equal(("2026-06-19", "2026-06-21"), Range(blocks["端午节"]));
            Assert.Equal(("2026-09-25", "2026-09-27"), Range(blocks["中秋节"]));
            Assert.Equal(("2026-10-01", "2026-10-07"), Range(blocks["国庆节"]));
            Assert.Equal("2026-01-04,2026-02-14,2026-02-28,2026-05-09,2026-09-20,2026-10-10", string.Join(',', data.MakeUpWorkdays.Select(d => d.ToString("yyyy-MM-dd"))));
        }

        static (string Start, string End) Range(HolidayBlock block) => (block.Start.ToString("yyyy-MM-dd"), block.End.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void WorkdayFollowsArrangementWhenDataExistsAndFallsBackToWeekdays()
    {
        StatutoryHolidays holidays = StatutoryHolidays.Instance;

        Assert.False(holidays.IsWorkday(new DateTime(2027, 10, 5)));      // 周二，国庆假期
        Assert.True(holidays.IsWorkday(new DateTime(2027, 10, 9)));       // 周六，调休补班
        Assert.False(holidays.IsWorkday(new DateTime(2027, 10, 10)));     // 周六，不在补班列表里
        Assert.False(holidays.IsWorkday(new DateTime(2027, 10, 4)));      // 周一，国庆假期
        Assert.True(holidays.IsWorkday(new DateTime(2028, 3, 1)));        // 2028 安排未公布：按周三算
        Assert.False(holidays.IsWorkday(new DateTime(2028, 3, 4)));       // 周六
    }

    [Fact]
    public void UnwrapsBase64ContentFromGitHubApi()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("""{"year":2027}""");
        string wrapped = $$"""{"name":"2027.json","encoding":"base64","content":"{{Convert.ToBase64String(bytes)}}"}""";

        Assert.Equal("""{"year":2027}""", StatutoryHolidays.UnwrapGitHubApi(wrapped));
        Assert.Null(StatutoryHolidays.UnwrapGitHubApi("""{"message":"Not Found","documentation_url":"https://example.invalid"}"""));
        Assert.Null(StatutoryHolidays.UnwrapGitHubApi("not json"));
    }
}
