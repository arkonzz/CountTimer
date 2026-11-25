using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace CountTimer.Service
{
    /// <summary>法定节假日安排里的一天：放假，或调休上班</summary>
    public sealed record StatutoryDay(DateTime Date, string Name, bool IsOffDay);

    /// <summary>连着的一段放假日期；Names 为数据源给这段里各天标注的节日名</summary>
    public sealed record HolidayBlock(DateTime Start, DateTime End, IReadOnlyList<string> Names);

    /// <summary>一年的法定节假日安排</summary>
    public sealed class StatutoryYear
    {
        private readonly HashSet<DateTime> offDays;
        private readonly HashSet<DateTime> makeUpWorkdays;

        private StatutoryYear(int year, List<StatutoryDay> days, List<HolidayBlock> blocks, List<DateTime> makeUpWorkdays)
        {
            Year = year;
            Days = days;
            OffBlocks = blocks;
            MakeUpWorkdays = makeUpWorkdays;
            offDays = days.Where(d => d.IsOffDay).Select(d => d.Date).ToHashSet();
            this.makeUpWorkdays = makeUpWorkdays.ToHashSet();
        }

        public int Year { get; }

        public IReadOnlyList<StatutoryDay> Days { get; }

        public IReadOnlyList<HolidayBlock> OffBlocks { get; }

        /// <summary>调休上班日</summary>
        public IReadOnlyList<DateTime> MakeUpWorkdays { get; }

        public bool IsOffDay(DateTime date) => date.Year == Year && offDays.Contains(date.Date);

        /// <summary>是否工作日：工作日以不放假为准，周末只有被列为补班日才算；不属于本年的日期为 false</summary>
        public bool IsWorkday(DateTime date)
        {
            if (date.Year != Year)
            {
                return false;
            }

            return date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                ? !offDays.Contains(date.Date)
                : makeUpWorkdays.Contains(date.Date);
        }

        /// <summary>
        /// 解析一年的安排。兼容两种文本：数据源的原始格式 {"year":2027,"days":[…]}，
        /// 以及本地缓存的纯数组 […]。内容来自网络或用户手动放入的文件，任何一项不合规都整体拒绝
        /// </summary>
        public static StatutoryYear? Parse(string json, int year)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("year", out JsonElement yearElement)
                        && (yearElement.ValueKind != JsonValueKind.Number || !yearElement.TryGetInt32(out int stated) || stated != year))
                    {
                        return null;
                    }

                    if (!root.TryGetProperty("days", out root))
                    {
                        return null;
                    }
                }

                if (root.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var days = new List<(DateTime Date, string Name, bool IsOffDay)>();
                foreach (JsonElement item in root.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || !item.TryGetProperty("date", out JsonElement dateElement)
                        || !item.TryGetProperty("name", out JsonElement nameElement)
                        || !item.TryGetProperty("isOffDay", out JsonElement offElement)
                        || dateElement.ValueKind != JsonValueKind.String
                        || nameElement.ValueKind != JsonValueKind.String
                        || (offElement.ValueKind != JsonValueKind.True && offElement.ValueKind != JsonValueKind.False))
                    {
                        return null;
                    }

                    string? name = nameElement.GetString();
                    if (!DateOnly.TryParseExact(dateElement.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
                        || date.Year != year
                        || string.IsNullOrWhiteSpace(name)
                        || name.Length > 40)
                    {
                        return null;
                    }

                    days.Add((date.ToDateTime(TimeOnly.MinValue), name, offElement.ValueKind == JsonValueKind.True));
                }

                return Create(year, days);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
            {
                return null;
            }
        }

        internal static StatutoryYear? Create(int year, List<(DateTime Date, string Name, bool IsOffDay)> days)
        {
            // 一年的安排通常 30~50 条；条数离谱、日期重复或没有任何放假日都视为坏数据
            if (days.Count is < 10 or > 200
                || days.Select(d => d.Date).Distinct().Count() != days.Count
                || days.Any(d => d.Date.Year != year))
            {
                return null;
            }

            List<(DateTime Date, string Name)> offDaysSorted = days
                .Where(d => d.IsOffDay)
                .OrderBy(d => d.Date)
                .Select(d => (d.Date, d.Name))
                .ToList();
            if (offDaysSorted.Count == 0)
            {
                return null;
            }

            var blocks = new List<HolidayBlock>();
            DateTime blockStart = DateTime.MinValue, previous = DateTime.MinValue;
            var blockNames = new List<string>();
            foreach ((DateTime date, string name) in offDaysSorted)
            {
                bool continues = previous != DateTime.MinValue && date == previous.AddDays(1);
                if (!continues && previous != DateTime.MinValue)
                {
                    blocks.Add(new HolidayBlock(blockStart, previous, blockNames.ToList()));
                    blockNames.Clear();
                }

                if (!continues)
                {
                    blockStart = date;
                }

                if (!blockNames.Contains(name))
                {
                    blockNames.Add(name);
                }

                previous = date;
            }

            blocks.Add(new HolidayBlock(blockStart, previous, blockNames.ToList()));
            return new StatutoryYear(
                year,
                days.Select(d => new StatutoryDay(d.Date, d.Name, d.IsOffDay)).OrderBy(d => d.Date).ToList(),
                blocks,
                days.Where(d => !d.IsOffDay).Select(d => d.Date).OrderBy(d => d.Date).ToList());
        }
    }
}
