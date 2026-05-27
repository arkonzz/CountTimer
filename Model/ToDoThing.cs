using AntdUI;
using SqlSugar;

namespace CountTimer.Model
{
    [SugarTable("to_do_list")]
    public class ToDoThing : NotifyProperty
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        [SugarColumn(ColumnName = "to_do_info")]
        public string toDoInfo { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "end_time")]
        public string endTime { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "is_mailed")]
        public bool isMailed { get; set; }
        [SugarColumn(ColumnName = "is_regular")]
        public bool isRegular { get; set;}

        public override string ToString()
        {
            return toDoInfo;
        }
    }
  
}
