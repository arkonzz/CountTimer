using SqlSugar;

namespace CountTimer.Model
{
    /// <summary>自定义的日子：生日或纪念日按公历或农历每年重复；发薪日、还款日这类每月重复</summary>
    [SugarTable("custom_day")]
    public class CustomDay
    {
        public const int KindBirthday = 0;
        public const int KindAnniversary = 1;
        /// <summary>每月重复：month 不用，day 为每月几号</summary>
        public const int KindMonthly = 2;

        /// <summary>每月重复的日子遇休息日（周末、法定放假）怎么办</summary>
        public const int AdjustEarlier = 0;
        public const int AdjustLater = 1;
        public const int AdjustNone = 2;

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
        /// <summary>遇休息日的处理方式：AdjustEarlier / AdjustLater / AdjustNone（仅每月重复用）</summary>
        [SugarColumn(ColumnName = "adjust")]
        public int adjust { get; set; }
    }
}
