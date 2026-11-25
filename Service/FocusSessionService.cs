using System;
using System.Collections.Generic;
using System.Linq;
using CountTimer.Model;
using CountTimer.Share;
using SqlSugar;

namespace CountTimer.Service
{
    public interface FocusSessionService
    {
        int AddFocusSession(FocusSession session);

        int GetTodayCount();

        int GetTodayMinutes();

        /// <summary>最近 days 天（含今天）每天完成的专注次数；键为 "yyyy-MM-dd"，每一天都会出现在结果里</summary>
        Dictionary<string, int> GetDailyCounts(int days);
    }

    public class FocusSessionServiceImpl : DbContext<FocusSession>, FocusSessionService
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public int AddFocusSession(FocusSession session)
        {
            return Db.Insertable(session).ExecuteCommand();
        }

        public int GetTodayCount()
        {
            string today = DateTime.Today.ToString(DateFormat);
            return Db.Queryable<FocusSession>().Count(it => it.sessionDate == today);
        }

        public int GetTodayMinutes()
        {
            string today = DateTime.Today.ToString(DateFormat);
            return Db.Queryable<FocusSession>()
                .Where(it => it.sessionDate == today)
                .Sum(it => it.focusMinutes);
        }

        public Dictionary<string, int> GetDailyCounts(int days)
        {
            // sessionDate 按 "yyyy-MM-dd" 文本存储；按这几天具体的日期值查询，结果里每天都有（没记录的为 0）
            string[] dates = Enumerable.Range(0, days)
                .Select(offset => DateTime.Today.AddDays(offset - (days - 1)).ToString(DateFormat))
                .ToArray();
            var counts = dates.ToDictionary(date => date, _ => 0);
            var grouped = Db.Queryable<FocusSession>()
                .Where(it => dates.Contains(it.sessionDate))
                .GroupBy(it => it.sessionDate)
                .Select(it => new { date = it.sessionDate, count = SqlFunc.AggregateCount(it.Id) })
                .ToList();
            foreach (var item in grouped)
            {
                counts[item.date] = item.count;
            }

            return counts;
        }
    }
}
