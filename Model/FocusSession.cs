using SqlSugar;

namespace CountTimer.Model
{
    /// <summary>
    /// 一次完成的专注记录（番茄钟）
    /// </summary>
    [SugarTable("focus_session")]
    public class FocusSession
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        [SugarColumn(ColumnName = "session_date")]
        public string sessionDate { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "end_time")]
        public string endTime { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "focus_minutes")]
        public int focusMinutes { get; set; }
    }
}
