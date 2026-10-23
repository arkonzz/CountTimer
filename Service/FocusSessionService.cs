using System;
using System.Collections.Generic;
using CountTimer.Model;
using CountTimer.Share;

namespace CountTimer.Service
{
    public interface FocusSessionService
    {
        int AddFocusSession(FocusSession session);

        int GetTodayCount();

        int GetTodayMinutes();
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
    }
}
