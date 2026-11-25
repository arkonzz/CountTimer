using System;

namespace CountTimer.Share
{
    /// <summary>应用界面语言</summary>
    public enum AppLanguage
    {
        Chinese,
        English
    }

    /// <summary>
    /// 轻量国际化：文案一律 T(中文, English) 内联双语，
    /// 切换语言后由主窗体统一广播 Changed，各控件 ApplyLanguage() 重刷静态文本；
    /// 动态拼接文本（气泡/状态栏/toast）在调用点实时取 T/F，无需注册 key
    /// </summary>
    public static class I18n
    {
        private static AppLanguage _language = AppLanguage.Chinese;

        public static AppLanguage Language => _language;

        public static bool IsEnglish => _language == AppLanguage.English;

        /// <summary>语言切换完成（UI 线程触发），订阅者应重刷自身静态文本</summary>
        public static event Action? Changed;

        /// <summary>启动时按持久化配置初始化，不触发 Changed</summary>
        public static void Init(AppLanguage language)
        {
            _language = language;
            ApplyAntdUiLanguage();
        }

        /// <summary>运行时切换语言（含持久化之外的广播）；重复设置同语言为空操作</summary>
        public static void SetLanguage(AppLanguage language)
        {
            if (_language == language)
            {
                return;
            }

            _language = language;
            ApplyAntdUiLanguage();
            Changed?.Invoke();
        }

        /// <summary>取当前语言文案</summary>
        public static string T(string zh, string en) => IsEnglish ? en : zh;

        /// <summary>格式化文案（带占位参数）</summary>
        public static string F(string zhFormat, string enFormat, params object[] args)
            => string.Format(IsEnglish ? enFormat : zhFormat, args);

        private static void ApplyAntdUiLanguage()
        {
            // AntdUI 内置控件（日期面板等）跟随切换
            AntdUI.Localization.SetLanguage(IsEnglish ? "en-US" : "zh-CN");
        }
    }
}
