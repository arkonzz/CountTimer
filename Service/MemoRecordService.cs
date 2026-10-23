using System;
using System.Collections.Generic;
using CountTimer.Model;
using CountTimer.Share;
using SqlSugar;

namespace CountTimer.Service
{
    public interface MemoRecordService
    {
        int AddMemoRecord(MemoRecord record);

        List<MemoRecord> GetMemoList();

        int DeleteMemoById(int id);

        int GetTodayCount();
    }

    public class MemoRecordServiceImpl : DbContext<MemoRecord>, MemoRecordService
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public int AddMemoRecord(MemoRecord record)
        {
            return Db.Insertable(record).ExecuteCommand();
        }

        public List<MemoRecord> GetMemoList()
        {
            return Db.Queryable<MemoRecord>()
                .OrderBy(it => it.recordTime, OrderByType.Desc)
                .OrderBy(it => it.Id, OrderByType.Desc)
                .ToList();
        }

        public int DeleteMemoById(int id)
        {
            return Db.Deleteable<MemoRecord>().In(id).ExecuteCommand();
        }

        public int GetTodayCount()
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            return Db.Queryable<MemoRecord>().Count(it => it.recordTime.StartsWith(today));
        }
    }
}
