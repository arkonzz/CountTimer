namespace CountTimer.Model
{
    /// <summary>
    /// 应用配置
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
        /// <summary>界面语言："zh" / "en"</summary>
        public string Language { get; set; } = "zh";
        /// <summary>窗口置顶</summary>
        public bool AlwaysOnTop { get; set; } = true;
        /// <summary>开机自启，启动后最小化到托盘</summary>
        public bool StartWithWindows { get; set; } = false;
        /// <summary>托盘图标显示番茄钟剩余时间</summary>
        public bool ShowTrayTime { get; set; } = true;
        /// <summary>全局开始/暂停快捷键，如 "Ctrl+Alt+Space"；空串为关闭</summary>
        public string Hotkey { get; set; } = "Ctrl+Alt+Space";
        /// <summary>上次关闭时的窗口位置与大小，0 表示未记录（未记录或屏幕上已不可见时回到右下角）</summary>
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
    }
}
