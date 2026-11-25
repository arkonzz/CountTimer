using SqlSugar;

namespace CountTimer.Model
{
    /// <summary>自定义的日子：生日或纪念日，按公历或农历每年重复</summary>
    [SugarTable("custom_day")]
    public class CustomDay
    {
        public const int KindBirthday = 0;
        public const int KindAnniversary = 1;

        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        [SugarColumn(ColumnName = "title")]
        public string title { get; set; } = string.Empty;
        /// <summary>KindBirthday 或 KindAnniversary</summary>
        [SugarColumn(ColumnName = "kind")]
        public int kind { get; set; }
        /// <summary>日期按农历算</summary>
        [SugarColumn(ColumnName = "is_lunar")]
        public bool isLunar { get; set; }
        [SugarColumn(ColumnName = "month")]
        public int month { get; set; }
        [SugarColumn(ColumnName = "day")]
        public int day { get; set; }
        /// <summary>起始年份（生日为出生年），0 表示不填</summary>
        [SugarColumn(ColumnName = "year")]
        public int year { get; set; }
    }
}
