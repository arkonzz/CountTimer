using CountTimer.Share;

namespace CountTimer.Model
{
    /// <summary>
    /// 节假日条目：节假日倒计时面板的数据模型（名称/标注中英双语）
    /// </summary>
    public class HolidayInfo
    {
        /// <summary>节日名称（中文）</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>节日名称（英文）</summary>
        public string NameEn { get; init; } = string.Empty;

        /// <summary>公历日期（当天 00:00）；有放假安排时为放假首日</summary>
        public DateTime Date { get; init; }

        /// <summary>最后一天（含）；单日节日为默认值，等同于 Date</summary>
        public DateTime EndDate { get; init; }

        /// <summary>日期后缀标注（中文），如"农历正月初一"、"放假 10.01–10.07（7 天）"；没有则为 null</summary>
        public string? Note { get; init; }

        /// <summary>日期后缀标注（英文）</summary>
        public string? NoteEn { get; init; }

        /// <summary>法定节日：有放假安排时并入对应的假期</summary>
        public bool IsStatutory { get; init; }

        /// <summary>最后一天（含）</summary>
        public DateTime LastDay => EndDate == default ? Date : EndDate;

        /// <summary>按当前界面语言取节日名</summary>
        public string DisplayName => I18n.T(Name, NameEn);

        /// <summary>按当前界面语言取标注</summary>
        public string? DisplayNote => Note is null ? null : I18n.T(Note, NoteEn ?? Note);

        /// <summary>今天是否在假期之中（首日已过、还没放完）</summary>
        public bool IsOngoing(DateTime today) => today.Date > Date && today.Date <= LastDay;
    }
}
