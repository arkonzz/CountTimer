using System;
using System.IO;
using System.Text.Json;
using CountTimer.Model;

namespace CountTimer.Service
{
    /// <summary>
    /// 应用配置读写（Db/settings.json）
    /// </summary>
    public class AppSettingsService
    {
        private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "Db", "settings.json");

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
        private static AppSettings Normalize(AppSettings settings)
        {
            settings.FocusMinutes = Math.Clamp(settings.FocusMinutes, 1, 240);
            settings.ShortBreakMinutes = Math.Clamp(settings.ShortBreakMinutes, 1, 120);
            settings.LongBreakMinutes = Math.Clamp(settings.LongBreakMinutes, 1, 180);
            settings.RoundsBeforeLongBreak = Math.Clamp(settings.RoundsBeforeLongBreak, 1, 12);
            return settings;
        }
    }
}
