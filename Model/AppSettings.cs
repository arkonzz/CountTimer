namespace CountTimer.Model
{
    /// <summary>
    /// 番茄钟配置
    /// </summary>
    public class AppSettings
    {
        public int FocusMinutes { get; set; } = 25;
        public int ShortBreakMinutes { get; set; } = 5;
        public int LongBreakMinutes { get; set; } = 15;
        public int RoundsBeforeLongBreak { get; set; } = 4;
        public bool AutoStartBreak { get; set; } = true;
        public bool AutoStartFocus { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;
        public bool DarkMode { get; set; } = false;
    }
}
