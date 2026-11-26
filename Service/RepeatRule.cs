using System;
using System.Collections.Generic;
using CountTimer.Model;

namespace CountTimer.Service
{
    /// <summary>重复事件的下一次发生时间</summary>
    public static class RepeatRule
    {
        /// <summary>最多往后找的天数：要能跨过春节这类连休</summary>
        private const int SearchDays = 40;

        /// <summary>
        /// now 之后（不含 now）的下一次发生时间。
        /// 时间取 timeOfDay；每周重复取 weekday，工作日按 isWorkday 判断
        /// </summary>
        public static DateTime Next(RepeatKind kind, int weekday, TimeSpan timeOfDay, DateTime now, Func<DateTime, bool>? isWorkday = null)
        {
            isWorkday ??= StatutoryHolidays.Instance.IsWorkday;
            DateTime today = now.Date;
            for (int offset = 0; offset < SearchDays; offset++)
            {
                DateTime date = today.AddDays(offset);
                if (offset == 0 && timeOfDay <= now.TimeOfDay)
                {
                    continue;
                }

                if (Matches(kind, weekday, date, isWorkday))
                {
                    return date.Add(timeOfDay);
                }
            }

            // 连续 40 天都不是工作日不可能是真实数据，退回每天
            return today.AddDays(1).Add(timeOfDay);
        }

        public static bool Matches(RepeatKind kind, int weekday, DateTime date, Func<DateTime, bool> isWorkday) => kind switch
        {
            RepeatKind.Weekly => (int)date.DayOfWeek == weekday,
            RepeatKind.Weekdays => isWorkday(date),
            _ => true,
        };

        /// <summary>
        /// 事件下一次发生的时间。循环事件存的就是下一次，后台还没来得及往前滚的现算；时间格式不对时为 null
        /// </summary>
        public static DateTime? NextOccurrence(ToDoThing thing, DateTime now)
        {
            if (!DateTime.TryParse(thing.endTime, out DateTime end))
            {
                return null;
            }

            if (thing.Kind != RepeatKind.None && end < now)
            {
                end = Next(thing.Kind, thing.repeatDay, end.TimeOfDay, now);
            }

            return end;
        }

        /// <summary>重复方式的显示文字："每天" / "工作日" / "每周三"，不重复为空；每周重复时 weekday 传那天是星期几</summary>
        internal static (string Zh, string En) Describe(RepeatKind kind, DayOfWeek? weekday = null)
        {
            switch (kind)
            {
                case RepeatKind.Daily:
                    return ("每天", "Daily");
                case RepeatKind.Weekdays:
                    return ("工作日", "Weekdays");
                case RepeatKind.Weekly when weekday is { } day:
                    return ($"每周{"日一二三四五六"[(int)day]}", $"Weekly {day}");
                default:
                    return (string.Empty, string.Empty);
            }
        }
    }
}
