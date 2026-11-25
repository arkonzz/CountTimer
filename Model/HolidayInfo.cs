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

        /// <summary>公历日期（当天 00:00）</summary>
        public DateTime Date { get; init; }

        /// <summary>日期后缀标注（中文），如"农历正月初一"、"节气"；公历固定节日为 null</summary>
        public string? Note { get; init; }

        /// <summary>日期后缀标注（英文）</summary>
        public string? NoteEn { get; init; }

        /// <summary>按当前界面语言取节日名</summary>
        public string DisplayName => I18n.T(Name, NameEn);

        /// <summary>按当前界面语言取标注</summary>
        public string? DisplayNote => Note is null ? null : I18n.T(Note, NoteEn ?? Note);
    }
}
