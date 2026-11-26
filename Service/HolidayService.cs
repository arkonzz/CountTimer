using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CountTimer.Model;

namespace CountTimer.Service
{
    /// <summary>
    /// 节假日计算：公历固定节日 + 农历节日（ChineseLunisolarCalendar 官方历表 1901~2100）
    /// + 清明（太阳视黄经天文算法，无需逐年硬编码）
    /// + 自定义生日/纪念日（按年重复）+ 发薪日这类每月重复的日子 + 法定放假与调休（并入对应节日）
    /// </summary>
    public static class HolidayService
    {
        private const double JulianDayJ2000 = 2451545.0;
        private static readonly DateTime J2000Utc = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private static readonly ChineseLunisolarCalendar Lunar = new();

        private static readonly string[] LunarMonthNames = { "正", "二", "三", "四", "五", "六", "七", "八", "九", "十", "冬", "腊" };
        private static readonly string[] DigitNames = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };

        /// <summary>Upcoming 固定截取的数量：约覆盖未来 14 个月，尾部按序截断，不会中途缺节</summary>
        private const int MaxUpcomingCount = 16;

        /// <summary>遇休息日调整时最多跨过的天数：要能跨过春节这类连休</summary>
        private const int MaxAdjustDays = 40;

        /// <summary>
        /// 以 today 为界把节日拆成两段（各自按日期升序）：
        /// Past 用于英雄卡"上个节日以来已过 N%"的进度计算，Upcoming 用于倒计时展示。
        /// customDays：自定义的生日/纪念日和每月重复的日子；statutory：按年取法定放假安排，没有则为 null；
        /// events：事件倒计时，每个只放下一次发生，只进 Upcoming
        /// </summary>
        public static (List<HolidayInfo> Past, List<HolidayInfo> Upcoming) GetAround(
            DateTime today,
            IReadOnlyList<CustomDay>? customDays = null,
            Func<int, StatutoryYear?>? statutory = null,
            IReadOnlyList<ToDoThing>? events = null)
        {
            // 多取上一年，保证元旦当天也有"上一个节日"可算进度；
            // 多生成到 Y+2：除夕按"农历次年春节前一天"计算，会领先落到公历次年 1 月
            // （如农历 2027 年除夕 = 2028-01-25），只生成到 Y+1 会出现"除夕在而元旦缺"的断档；
            // 最终按 MaxUpcomingCount 截断，列表尾部永远连续
            int fromYear = today.Year - 1, toYear = today.Year + 2;
            List<HolidayInfo> list = BaseHolidays(fromYear, toYear);
            if (customDays is not null)
            {
                foreach (CustomDay day in customDays)
                {
                    if (day.kind == CustomDay.KindMonthly)
                    {
                        AddNearestMonthlyDays(list, day, today, fromYear, toYear, statutory);
                        continue;
                    }

                    for (int year = fromYear; year <= toYear; year++)
                    {
                        AddCustomDay(list, day, year);
                    }
                }
            }

            if (events is not null)
            {
                AddEvents(list, events, today);
            }

            if (statutory is not null)
            {
                ApplyArrangements(list, statutory);
            }

            // 同一天既有节日又有自定义的日子时节日在前，其次是每月重复的日子，最后是事件：英雄卡优先显示节日
            list = list.OrderBy(h => h.Date).ThenBy(SameDayRank).ToList();
            // 假期首日已过但还没放完的，仍然算在"未来"里
            var upcoming = list.FindAll(h => h.LastDay >= today.Date);
            if (upcoming.Count > MaxUpcomingCount)
            {
                upcoming.RemoveRange(MaxUpcomingCount, upcoming.Count - MaxUpcomingCount);
            }

            return (list.FindAll(h => h.LastDay < today.Date), upcoming);
        }

        /// <summary>取 today（含）起的未来节日列表（不含自定义日子与放假安排），覆盖未来一整年</summary>
        public static List<HolidayInfo> GetUpcoming(DateTime today) => GetAround(today).Upcoming;

        private static int SameDayRank(HolidayInfo h) => h.IsEvent ? 2 : h.IsMonthly ? 1 : 0;

        /// <summary>
        /// 英雄卡显示的那一条：列表里第一个不是循环事件的条目。每天 / 工作日 / 每周循环的事件
        /// 永远排在未来一两天，让它上英雄卡的话大号倒计时就永远是"今天 / 明天"
        /// </summary>
        public static HolidayInfo? PickHero(IReadOnlyList<HolidayInfo> upcoming)
            => upcoming.FirstOrDefault(h => !h.IsRecurringEvent);

        /// <summary>内置节日：公历固定 + 农历 + 清明。超出农历历表范围的年份跳过农历项</summary>
        internal static List<HolidayInfo> BaseHolidays(int fromYear, int toYear)
        {
            var list = new List<HolidayInfo>();
            for (int year = fromYear; year <= toYear; year++)
            {
                AddFixed(list, "元旦", "New Year's Day", year, 1, 1, isStatutory: true);
                TryAdd(list, () => AddLunar(list, "春节", "Spring Festival", year, 1, 1, isStatutory: true));
                TryAdd(list, () => AddLunar(list, "元宵节", "Lantern Festival", year, 1, 15, isStatutory: false));
                TryAdd(list, () => AddChuxi(list, year));
                TryAdd(list, () => AddQingming(list, year));
                AddFixed(list, "劳动节", "Labour Day", year, 5, 1, isStatutory: true);
                TryAdd(list, () => AddLunar(list, "端午节", "Dragon Boat Festival", year, 5, 5, isStatutory: true));
                AddFixed(list, "儿童节", "Children's Day", year, 6, 1, isStatutory: false);
                TryAdd(list, () => AddLunar(list, "七夕节", "Qixi Festival", year, 7, 7, isStatutory: false));
                TryAdd(list, () => AddLunar(list, "中秋节", "Mid-Autumn Festival", year, 8, 15, isStatutory: true));
                AddFixed(list, "国庆节", "National Day", year, 10, 1, isStatutory: true);
                TryAdd(list, () => AddLunar(list, "重阳节", "Double Ninth Festival", year, 9, 9, isStatutory: false));
            }

            return list;
        }

        /// <summary>农历历表只到 2100 年，越界的年份整项跳过</summary>
        private static void TryAdd(List<HolidayInfo> list, Action add)
        {
            try
            {
                add();
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        /// <summary>
        /// 把法定放假安排并入列表：落在某段假期内的法定节日不再单独出现，
        /// 改为以那段假期为一条（倒计时到放假首日，标注放几天、哪几天补班）
        /// </summary>
        private static void ApplyArrangements(List<HolidayInfo> list, Func<int, StatutoryYear?> getYear)
        {
            var absorbed = new HashSet<HolidayInfo>();
            var arrangements = new List<HolidayInfo>();
            // 数据源里的节日名 → 英文名（取自内置条目，对不上就沿用中文）
            Dictionary<string, string> englishNames = list.GroupBy(h => h.Name).ToDictionary(g => g.Key, g => g.First().NameEn);

            foreach (int year in list.Select(h => h.Date.Year).Distinct())
            {
                StatutoryYear? data = getYear(year);
                if (data is null)
                {
                    continue;
                }

                foreach (HolidayInfo entry in list.Where(h => h.Date.Year == year && h.IsStatutory))
                {
                    if (data.OffBlocks.Any(block => entry.Date >= block.Start && entry.Date <= block.End))
                    {
                        absorbed.Add(entry);
                    }
                }

                foreach (HolidayBlock block in data.OffBlocks)
                {
                    (string zh, string en) = DescribeArrangement(block, data);
                    arrangements.Add(new HolidayInfo
                    {
                        Name = string.Join("·", block.Names),
                        NameEn = string.Join(" & ", block.Names.Select(name => englishNames.GetValueOrDefault(name, name))),
                        Date = block.Start,
                        EndDate = block.End,
                        Note = zh,
                        NoteEn = en,
                        IsStatutory = true,
                    });
                }
            }

            list.RemoveAll(absorbed.Contains);
            list.AddRange(arrangements);
        }

        private static (string Zh, string En) DescribeArrangement(HolidayBlock block, StatutoryYear data)
        {
            int span = (block.End - block.Start).Days + 1;
            List<DateTime> makeUpDays = data.Days
                .Where(day => !day.IsOffDay && block.Names.Contains(day.Name))
                .Select(day => day.Date)
                .ToList();

            string zh = span == 1 ? "放假 1 天" : $"放假 {Short(block.Start)}–{Short(block.End)}（{span} 天）";
            string en = span == 1 ? "1 day off" : $"Off {Medium(block.Start)}–{Medium(block.End)} · {span} days";
            if (makeUpDays.Count > 0)
            {
                zh += $" · 补班 {string.Join("、", makeUpDays.Select(Short))}";
                en += $" · Work {string.Join(", ", makeUpDays.Select(Medium))}";
            }

            return (zh, en);
        }

        private static string Short(DateTime date) => $"{date.Month}.{date.Day}";

        private static string Medium(DateTime date) => date.ToString("MMM d", CultureInfo.InvariantCulture);

        private static void AddFixed(List<HolidayInfo> list, string name, string nameEn, int year, int month, int day, bool isStatutory)
        {
            list.Add(new HolidayInfo { Name = name, NameEn = nameEn, Date = new DateTime(year, month, day), IsStatutory = isStatutory });
        }

        private static void AddLunar(List<HolidayInfo> list, string name, string nameEn, int lunarYear, int lunarMonth, int lunarDay, bool isStatutory)
        {
            DateTime date = LunarToSolar(lunarYear, lunarMonth, lunarDay);
            (string zh, string en) = LunarDescribe(date);
            list.Add(new HolidayInfo { Name = name, NameEn = nameEn, Date = date, Note = zh, NoteEn = en, IsStatutory = isStatutory });
        }

        /// <summary>除夕 = 该农历年次年正月初一的前一天（腊月可能只有 29 天，按真实日名标注）</summary>
        private static void AddChuxi(List<HolidayInfo> list, int lunarYear)
        {
            DateTime date = LunarToSolar(lunarYear + 1, 1, 1).AddDays(-1);
            (string zh, string en) = LunarDescribe(date);
            list.Add(new HolidayInfo
            {
                Name = "除夕",
                NameEn = "Chinese New Year's Eve",
                Date = date,
                Note = zh,
                NoteEn = en,
                IsStatutory = true,
            });
        }

        #region 自定义生日 / 纪念日

        /// <summary>自定义日子在某一年的公历日期；月份不合法或超出农历历表时为 null。
        /// 农历三十在当月只有廿九的年份落到廿九，公历 2 月 29 日在平年落到 2 月 28 日</summary>
        internal static DateTime? CustomDayDate(CustomDay day, int year)
        {
            if (day.month is < 1 or > 12 || day.day < 1 || day.day > 31)
            {
                return null;
            }

            try
            {
                return day.isLunar
                    ? LunarToSolar(year, day.month, Math.Min(day.day, DaysInLunarMonth(year, day.month)))
                    : new DateTime(year, day.month, Math.Min(day.day, DateTime.DaysInMonth(year, day.month)));
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>自定义日子从 today（含）起的下一次发生日期</summary>
        public static DateTime? NextCustomDay(CustomDay day, DateTime today, Func<int, StatutoryYear?>? statutory = null)
        {
            if (day.kind == CustomDay.KindMonthly)
            {
                // 遇休息日提前的那一次可能落在上个月，所以逐月都看一遍，取最早的一次
                DateTime? next = null;
                for (int offset = 0; offset <= 12; offset++)
                {
                    DateTime month = today.AddMonths(offset);
                    if (MonthlyDayDate(day, month.Year, month.Month, WorkdayResolver(statutory)) is DateTime date
                        && date >= today.Date
                        && (next is null || date < next))
                    {
                        next = date;
                    }
                }

                return next;
            }

            for (int year = today.Year; year <= today.Year + 1; year++)
            {
                if (CustomDayDate(day, year) is DateTime date && date >= today.Date)
                {
                    return date;
                }
            }

            return null;
        }

        private static void AddCustomDay(List<HolidayInfo> list, CustomDay day, int year)
        {
            string title = day.title.Trim();
            // 起始年份之前的 occurrence 不存在（比如明年才到的纪念日）
            if (title.Length == 0 || year < day.year || CustomDayDate(day, year) is not DateTime date)
            {
                return;
            }

            bool isBirthday = day.kind != CustomDay.KindAnniversary;
            int since = day.year > 0 ? year - day.year : 0;
            var zh = new List<string> { isBirthday ? "生日" : "纪念日" };
            var en = new List<string> { isBirthday ? "Birthday" : "Anniversary" };
            if (day.isLunar)
            {
                (string lunarZh, string lunarEn) = LunarDescribe(date);
                zh.Add(lunarZh);
                en.Add(lunarEn);
            }

            if (since > 0)
            {
                zh.Add(isBirthday ? $"{since} 岁" : $"{since} 周年");
                en.Add(isBirthday ? $"turning {since}" : $"{since} yrs");
            }

            list.Add(new HolidayInfo
            {
                Name = title,
                NameEn = title,
                Date = date,
                Note = string.Join(" · ", zh),
                NoteEn = string.Join(" · ", en),
            });
        }

        #endregion

        #region 事件倒计时

        /// <summary>每个事件只放下一次发生。循环事件过去一次后数据里就不留上一次了，所以事件不进 Past</summary>
        private static void AddEvents(List<HolidayInfo> list, IReadOnlyList<ToDoThing> events, DateTime today)
        {
            DateTime now = DateTime.Now;
            foreach (ToDoThing thing in events)
            {
                string title = thing.toDoInfo.Trim();
                // 一次性事件过去后会被后台清掉，这里不再显示
                if (title.Length == 0
                    || RepeatRule.NextOccurrence(thing, now) is not DateTime next
                    || next.Date < today.Date)
                {
                    continue;
                }

                // 标注写时刻，循环的再带上重复方式："09:00 · 每天"
                string zh = next.ToString("HH:mm"), en = next.ToString("h:mm tt", CultureInfo.InvariantCulture);
                (string repeatZh, string repeatEn) = RepeatRule.Describe(thing.Kind, next.DayOfWeek);
                if (repeatZh.Length > 0)
                {
                    zh += $" · {repeatZh}";
                    en += $" · {repeatEn}";
                }

                list.Add(new HolidayInfo
                {
                    Name = title,
                    NameEn = title,
                    Date = next.Date,
                    Note = zh,
                    NoteEn = en,
                    IsEvent = true,
                    IsRecurringEvent = thing.Kind != RepeatKind.None,
                    Time = next,
                });
            }
        }

        /// <summary>今天之内还没到点的事件还剩多久："还有 2 小时 13 分"；不到一小时只说分，不到一分钟是"马上"</summary>
        internal static (string Zh, string En) TimeLeft(DateTime target, DateTime now)
        {
            TimeSpan left = target - now;
            if (left.TotalMinutes < 1)
            {
                return ("马上", "any moment");
            }

            int hours = (int)left.TotalHours;
            return hours > 0
                ? ($"还有 {hours} 小时 {left.Minutes} 分", $"{hours} h {left.Minutes} min to go")
                : ($"还有 {left.Minutes} 分", $"{left.Minutes} min to go");
        }

        /// <summary>今天之内、还没到点的事件在界面上显示的剩余时间；其他条目为 null，照常显示还有几天</summary>
        internal static (string Zh, string En)? TimeLeftToday(HolidayInfo holiday, DateTime now)
        {
            return holiday.Time is DateTime time && time.Date == now.Date && time > now
                ? TimeLeft(time, now)
                : null;
        }

        #endregion

        #region 每月重复的日子（发薪日、还款日这类）

        /// <summary>
        /// 每月重复的日子在 year 年 month 月的实际日期；几号不合法时为 null。
        /// 当月没有这一天（29、30、31）时取月末；遇休息日按 adjust 提前或顺延到最近的工作日
        /// </summary>
        internal static DateTime? MonthlyDayDate(CustomDay day, int year, int month, Func<DateTime, bool>? isWorkday = null)
            => MonthlyOccurrence(day, year, month, isWorkday ?? StatutoryHolidays.Instance.IsWorkday)?.Date;

        /// <summary>
        /// 只把"上一次"和"下一次"两次发生加进列表：每月重复的日子一年有 12 次，
        /// 全加进来会把节日挤出固定长度的列表
        /// </summary>
        private static void AddNearestMonthlyDays(
            List<HolidayInfo> list, CustomDay day, DateTime today, int fromYear, int toYear, Func<int, StatutoryYear?>? statutory)
        {
            if (day.title.Trim().Length == 0 || day.day is < 1 or > 31)
            {
                return;
            }

            Func<DateTime, bool> isWorkday = WorkdayResolver(statutory);
            HolidayInfo? previous = null;
            for (int year = fromYear; year <= toYear; year++)
            {
                for (int month = 1; month <= 12; month++)
                {
                    if (MonthlyOccurrence(day, year, month, isWorkday) is not { } occurrence)
                    {
                        return;
                    }

                    // 日期逐月递增：第一个不早于今天的就是下一次，它前面那一个就是上一次
                    if (occurrence.Date >= today.Date)
                    {
                        if (previous is not null)
                        {
                            list.Add(previous);
                        }

                        list.Add(MonthlyEntry(day, occurrence));
                        return;
                    }

                    previous = MonthlyEntry(day, occurrence);
                }
            }
        }

        private static HolidayInfo MonthlyEntry(CustomDay day, (DateTime Nominal, DateTime Date) occurrence)
        {
            (string zh, string en) = MonthlyRule(day);
            // 日期和"每月几号"对不上时说明原因
            if (occurrence.Date != occurrence.Nominal)
            {
                zh += $"（原{occurrence.Nominal.Month}月{occurrence.Nominal.Day}日）";
                en += $" (was {occurrence.Nominal.ToString("MMM d", CultureInfo.InvariantCulture)})";
            }

            return new HolidayInfo
            {
                Name = day.title.Trim(),
                NameEn = day.title.Trim(),
                Date = occurrence.Date,
                Note = zh,
                NoteEn = en,
                IsMonthly = true,
            };
        }

        /// <summary>"每月10日"，遇休息日会挪动的再带上挪动方式；管理表格的日期列和节日列表都用它</summary>
        internal static (string Zh, string En) MonthlyRule(CustomDay day)
        {
            string ordinal = day.day switch { 1 or 21 or 31 => "st", 2 or 22 => "nd", 3 or 23 => "rd", _ => "th" };
            // 英文要短：管理表格的日期列只有 176 像素宽
            return day.adjust switch
            {
                CustomDay.AdjustLater => ($"每月{day.day}日 · 遇休息日顺延", $"{day.day}{ordinal} monthly · later if off"),
                CustomDay.AdjustNone => ($"每月{day.day}日", $"{day.day}{ordinal} monthly"),
                _ => ($"每月{day.day}日 · 遇休息日提前", $"{day.day}{ordinal} monthly · earlier if off"),
            };
        }

        /// <summary>某个月的原始日期和遇休息日挪动后的日期</summary>
        private static (DateTime Nominal, DateTime Date)? MonthlyOccurrence(CustomDay day, int year, int month, Func<DateTime, bool> isWorkday)
        {
            if (day.day is < 1 or > 31)
            {
                return null;
            }

            DateTime nominal = new(year, month, Math.Min(day.day, DateTime.DaysInMonth(year, month)));
            DateTime date = nominal;
            int step = day.adjust switch
            {
                CustomDay.AdjustLater => 1,
                CustomDay.AdjustNone => 0,
                _ => -1,
            };

            // 连续 40 天都不是工作日不可能是真实数据，到此为止
            for (int moved = 0; step != 0 && moved < MaxAdjustDays && !isWorkday(date); moved++)
            {
                date = date.AddDays(step);
            }

            return (nominal, date);
        }

        /// <summary>按法定放假安排判断工作日（周末补班算工作日，工作日放假不算）；没有当年安排时按周一至周五</summary>
        private static Func<DateTime, bool> WorkdayResolver(Func<int, StatutoryYear?>? statutory)
            => date => StatutoryYear.IsWorkday(date, statutory?.Invoke(date.Year));

        #endregion

        #region 农历换算

        /// <summary>农历（平月序号，非闰月）指定日 → 公历日期</summary>
        private static DateTime LunarToSolar(int lunarYear, int lunarMonth, int lunarDay)
            => Lunar.ToDateTime(lunarYear, LunarMonthIndex(lunarYear, lunarMonth), lunarDay, 0, 0, 0, 0, ChineseLunisolarCalendar.CurrentEra);

        /// <summary>
        /// 平月序号 → 历表里的月份序号。GetLeapMonth 返回闰月所在位置序号（1~13）：
        /// 该序号的月份即"闰(序号-1)月"，其后月份的序号整体 +1，因此平月序号 >= 闰月位置时需再进一位
        /// </summary>
        private static int LunarMonthIndex(int lunarYear, int lunarMonth)
        {
            int leapIndex = Lunar.GetLeapMonth(lunarYear);
            return leapIndex > 0 && lunarMonth >= leapIndex ? lunarMonth + 1 : lunarMonth;
        }

        private static int DaysInLunarMonth(int lunarYear, int lunarMonth)
            => Lunar.GetDaysInMonth(lunarYear, LunarMonthIndex(lunarYear, lunarMonth), ChineseLunisolarCalendar.CurrentEra);

        /// <summary>公历日期 → 农历标注（中文"农历X月XX日"，英文"Lunar M/D"，含闰月，取自真实历表）</summary>
        private static (string Zh, string En) LunarDescribe(DateTime solar)
        {
            int year = Lunar.GetYear(solar);
            int monthIndex = Lunar.GetMonth(solar);
            int day = Lunar.GetDayOfMonth(solar);

            int leapIndex = Lunar.GetLeapMonth(year);
            bool isLeap = leapIndex > 0 && monthIndex == leapIndex;
            int month = isLeap ? leapIndex - 1 : (leapIndex > 0 && monthIndex > leapIndex ? monthIndex - 1 : monthIndex);

            string monthName = (isLeap ? "闰" : "") + LunarMonthNames[month - 1] + "月";
            string leapPrefix = isLeap ? "leap " : "";
            return ($"农历{monthName}{LunarDayName(day)}", $"Lunar {leapPrefix}{month}/{day}");
        }

        private static string LunarDayName(int day)
        {
            if (day == 10) return "初十";
            if (day == 20) return "二十";
            if (day == 30) return "三十";
            if (day < 10) return "初" + DigitNames[day];
            if (day < 20) return "十" + DigitNames[day - 10];
            return "廿" + DigitNames[day - 20];
        }

        #endregion

        #region 清明（天文算法）

        /// <summary>
        /// 清明：太阳视黄经到达 15° 的时刻（北京时间）所在公历日。
        /// Meeus 低精度太阳黄经（误差约 0.01°≈15 分钟），足够确定日期，1901~2100 年均适用
        /// </summary>
        private static void AddQingming(List<HolidayInfo> list, int year)
        {
            const double QingmingLongitude = 15.0;

            // 粗估：春分约 3 月 20 日，太阳黄经每度约 1.01456 天 → 清明约 4 月 4 日
            DateTime guess = new DateTime(year, 3, 20).AddDays(QingmingLongitude * 365.2422 / 360);
            // 黄经按日递增约 0.9856°，前后各 8 天窗口内恰好跨越一次且单调
            double lo = ToJulianDay(guess.AddDays(-8).AddHours(-8));
            double hi = ToJulianDay(guess.AddDays(8).AddHours(-8));

            for (int i = 0; i < 60; i++)
            {
                double mid = (lo + hi) / 2;
                if (IsBeforeTarget(QingmingLongitude, SunApparentLongitude(mid)))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            DateTime beijing = FromJulianDay((lo + hi) / 2).AddHours(8);
            list.Add(new HolidayInfo
            {
                Name = "清明节",
                NameEn = "Qingming Festival",
                Date = beijing.Date,
                Note = "节气",
                NoteEn = "Solar term",
                IsStatutory = true,
            });
        }

        /// <summary>太阳黄经尚未到达 target（差值落在 -180°~180° 内为正）</summary>
        private static bool IsBeforeTarget(double targetDeg, double currentDeg)
        {
            double diff = (targetDeg - currentDeg + 540) % 360 - 180;
            return diff > 0;
        }

        /// <summary>Meeus 低精度太阳视黄经（度）</summary>
        private static double SunApparentLongitude(double julianDay)
        {
            double t = (julianDay - JulianDayJ2000) / 36525;
            double rad = Math.PI / 180;

            double meanLongitude = Norm360(280.46646 + 36000.76983 * t + 0.0003032 * t * t);
            double meanAnomaly = Norm360(357.52911 + 35999.05029 * t - 0.0001537 * t * t) * rad;
            double center = (1.914602 - 0.004817 * t - 0.000014 * t * t) * Math.Sin(meanAnomaly)
                          + (0.019993 - 0.000101 * t) * Math.Sin(2 * meanAnomaly)
                          + 0.000289 * Math.Sin(3 * meanAnomaly);
            double omega = Norm360(125.04 - 1934.136 * t) * rad;

            return Norm360(meanLongitude + center - 0.00569 - 0.00478 * Math.Sin(omega));
        }

        private static double Norm360(double deg) => deg - Math.Floor(deg / 360) * 360;

        private static double ToJulianDay(DateTime utc) => (utc - J2000Utc).TotalDays + JulianDayJ2000;

        private static DateTime FromJulianDay(double julianDay) => J2000Utc.AddDays(julianDay - JulianDayJ2000);

        #endregion
    }
}
