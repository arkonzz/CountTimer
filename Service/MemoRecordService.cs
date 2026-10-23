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

        List<MemoRecord> GetMemoList(DateTime? date = null);

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

        /// <summary>获取记录流水；传 date 只取当天，null 取全部</summary>
        public List<MemoRecord> GetMemoList(DateTime? date = null)
        {
            var query = Db.Queryable<MemoRecord>();
            if (date.HasValue)
            {
                // recordTime 按 "yyyy-MM-dd HH:mm:ss" 文本存储，用日期前缀过滤
                string prefix = date.Value.ToString("yyyy-MM-dd");
                query = query.Where(it => it.recordTime.StartsWith(prefix));
            }

            return query
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
