using SqlSugar;

namespace CountTimer.Model
{
    /// <summary>
    /// 小计记录：用户手动记录的事件流水
    /// </summary>
    [SugarTable("memo_record")]
    public class MemoRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        [SugarColumn(ColumnName = "content")]
        public string content { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "record_time")]
        public string recordTime { get; set; } = string.Empty;
    }
}
