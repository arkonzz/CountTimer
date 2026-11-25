using System;
using System.IO;
using System.Text.Json;
using CountTimer.Model;
using CountTimer.Share;

namespace CountTimer.Service
{
    /// <summary>
    /// 应用配置读写（Db/settings.json）
    /// </summary>
    public class AppSettingsService
    {
        private static readonly string FilePath = AppData.SettingsPath;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                    if (settings != null)
                    {
                        return Normalize(settings);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
            }

            return Normalize(new AppSettings());
        }

        public void Save(AppSettings settings)
        {
            settings = Normalize(settings);
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }

        /// <summary>
        /// 防止配置文件被改出非法值
        /// </summary>
        internal static AppSettings Normalize(AppSettings settings)
        {
            settings.FocusMinutes = Math.Clamp(settings.FocusMinutes, 1, 240);
            settings.ShortBreakMinutes = Math.Clamp(settings.ShortBreakMinutes, 1, 120);
            settings.LongBreakMinutes = Math.Clamp(settings.LongBreakMinutes, 1, 180);
            settings.RoundsBeforeLongBreak = Math.Clamp(settings.RoundsBeforeLongBreak, 1, 12);
            settings.Language = settings.Language == "en" ? "en" : "zh";
            // 存坏的快捷键直接关闭，避免启动时按残缺的组合去注册
            if (settings.Hotkey.Length > 0 && (!Hotkey.TryParse(settings.Hotkey, out Hotkey hotkey) || !hotkey.IsValid))
            {
                settings.Hotkey = string.Empty;
            }

            return settings;
        }
    }
}
