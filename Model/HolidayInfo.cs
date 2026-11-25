namespace CountTimer.Model
{
    /// <summary>
    /// 节假日条目：节假日倒计时面板的数据模型
    /// </summary>
    public class HolidayInfo
    {
        /// <summary>节日名称</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>公历日期（当天 00:00）</summary>
        public DateTime Date { get; init; }

        /// <summary>日期后缀标注，如"农历正月初一"、"节气"；公历固定节日为 null</summary>
        public string? Note { get; init; }
    }
}
