using System;
using System.Collections.Generic;
using System.Globalization;
using CountTimer.Model;

namespace CountTimer.Service
{
    /// <summary>
    /// 节假日计算（纯计算，无持久化）：
    /// 公历固定节日 + 农历节日（ChineseLunisolarCalendar 官方历表 1901~2100）
    /// + 清明（太阳视黄经天文算法，无需逐年硬编码）
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

        /// <summary>
        /// 以 today 为界把未来节日拆成两段（各自按日期升序）：
        /// Past 用于英雄卡"上个节日以来已过 N%"的进度计算，Upcoming 用于倒计时展示
        /// </summary>
        public static (List<HolidayInfo> Past, List<HolidayInfo> Upcoming) GetAround(DateTime today)
        {
            var list = new List<HolidayInfo>();
            // 多取上一年，保证元旦当天也有"上一个节日"可算进度；
            // 多生成到 Y+2：除夕按"农历次年春节前一天"计算，会领先落到公历次年 1 月
            // （如农历 2027 年除夕 = 2028-01-25），只生成到 Y+1 会出现"除夕在而元旦缺"的断档；
            // 最终按 MaxUpcomingCount 截断，列表尾部永远连续
            for (int year = today.Year - 1; year <= today.Year + 2; year++)
            {
                AddFixed(list, "元旦", year, 1, 1);
                AddLunar(list, "春节", year, 1, 1);
                AddLunar(list, "元宵节", year, 1, 15);
                AddChuxi(list, year);
                AddQingming(list, year);
                AddFixed(list, "劳动节", year, 5, 1);
                AddLunar(list, "端午节", year, 5, 5);
                AddFixed(list, "儿童节", year, 6, 1);
                AddLunar(list, "七夕节", year, 7, 7);
                AddLunar(list, "中秋节", year, 8, 15);
                AddFixed(list, "国庆节", year, 10, 1);
                AddLunar(list, "重阳节", year, 9, 9);
            }

            list.Sort((a, b) => a.Date.CompareTo(b.Date));
            var upcoming = list.FindAll(h => h.Date >= today.Date);
            if (upcoming.Count > MaxUpcomingCount)
            {
                upcoming.RemoveRange(MaxUpcomingCount, upcoming.Count - MaxUpcomingCount);
            }
            return (list.FindAll(h => h.Date < today.Date), upcoming);
        }

        /// <summary>取 today（含）起的未来节日列表，覆盖未来一整年，按日期升序</summary>
        public static List<HolidayInfo> GetUpcoming(DateTime today) => GetAround(today).Upcoming;

        private static void AddFixed(List<HolidayInfo> list, string name, int year, int month, int day)
        {
            list.Add(new HolidayInfo { Name = name, Date = new DateTime(year, month, day) });
        }

        private static void AddLunar(List<HolidayInfo> list, string name, int lunarYear, int lunarMonth, int lunarDay)
        {
            DateTime date = LunarToSolar(lunarYear, lunarMonth, lunarDay);
            list.Add(new HolidayInfo { Name = name, Date = date, Note = LunarDescribe(date) });
        }

        /// <summary>除夕 = 该农历年次年正月初一的前一天（腊月可能只有 29 天，按真实日名标注）</summary>
        private static void AddChuxi(List<HolidayInfo> list, int lunarYear)
        {
            DateTime date = LunarToSolar(lunarYear + 1, 1, 1).AddDays(-1);
            list.Add(new HolidayInfo { Name = "除夕", Date = date, Note = LunarDescribe(date) });
        }

        /// <summary>农历（平月序号，非闰月）指定日 → 公历日期</summary>
        private static DateTime LunarToSolar(int lunarYear, int lunarMonth, int lunarDay)
        {
            // GetLeapMonth 返回闰月所在位置序号（1~13）：该序号的月份即"闰(序号-1)月"，
            // 其后月份的序号整体 +1，因此平月序号 >= 闰月位置时需再进一位
            int leapIndex = Lunar.GetLeapMonth(lunarYear);
            int monthIndex = leapIndex > 0 && lunarMonth >= leapIndex ? lunarMonth + 1 : lunarMonth;
            return Lunar.ToDateTime(lunarYear, monthIndex, lunarDay, 0, 0, 0, 0, ChineseLunisolarCalendar.CurrentEra);
        }

        /// <summary>公历日期 → "农历X月XX日"（含闰月，取自真实历表）</summary>
        private static string LunarDescribe(DateTime solar)
        {
            int year = Lunar.GetYear(solar);
            int monthIndex = Lunar.GetMonth(solar);
            int day = Lunar.GetDayOfMonth(solar);

            int leapIndex = Lunar.GetLeapMonth(year);
            bool isLeap = leapIndex > 0 && monthIndex == leapIndex;
            int month = isLeap ? leapIndex - 1 : (leapIndex > 0 && monthIndex > leapIndex ? monthIndex - 1 : monthIndex);

            string monthName = (isLeap ? "闰" : "") + LunarMonthNames[month - 1] + "月";
            return $"农历{monthName}{LunarDayName(day)}";
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
            list.Add(new HolidayInfo { Name = "清明节", Date = beijing.Date, Note = "节气" });
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
    }
}
