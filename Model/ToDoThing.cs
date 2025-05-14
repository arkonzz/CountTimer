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
        public string toDoInfo { get; set; }
        [SugarColumn(ColumnName = "end_time")]
        public string endTime { get; set; }
        [SugarColumn(ColumnName = "is_mailed")]
        public bool isMailed { get; set; }
        }
  
}
