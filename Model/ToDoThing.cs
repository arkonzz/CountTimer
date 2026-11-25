using AntdUI;
using CountTimer.Share;
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
        public bool isRegular { get; set; }
        /// <summary>重复方式，见 RepeatKind。旧数据只有 isRegular，为真时视为每天</summary>
        [SugarColumn(ColumnName = "repeat_kind")]
        public int repeatKind { get; set; }
        /// <summary>每周重复时的星期（0 = 周日 … 6 = 周六）</summary>
        [SugarColumn(ColumnName = "repeat_day")]
        public int repeatDay { get; set; }

        /// <summary>生效的重复方式（兼容只写了 isRegular 的旧数据）</summary>
        public RepeatKind Kind => repeatKind != 0
            ? (RepeatKind)repeatKind
            : (isRegular ? RepeatKind.Daily : RepeatKind.None);

        public override string ToString()
        {
            string label = Kind switch
            {
                RepeatKind.Daily => I18n.T("每天", "Daily"),
                RepeatKind.Weekdays => I18n.T("工作日", "Weekdays"),
                RepeatKind.Weekly => I18n.T("每周", "Weekly") + WeekdayName((DayOfWeek)repeatDay),
                _ => string.Empty,
            };
            return label.Length == 0 ? toDoInfo : $"{toDoInfo}（{label}）";
        }

        private static string WeekdayName(DayOfWeek day) => I18n.IsEnglish
            ? " " + day.ToString()
            : "日一二三四五六"[(int)day].ToString();
    }
}
