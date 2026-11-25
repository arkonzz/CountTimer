using System;
using System.IO;

namespace CountTimer.Share
{
    /// <summary>
    /// 用户数据目录：%LOCALAPPDATA%\CountTimer（数据库、配置、法定节假日数据缓存）。
    /// 旧版本把数据放在程序目录的 Db\ 下，删 bin 或切换编译配置后就是一份全新的空数据，
    /// 首次启动时自动把旧数据搬过来。设置环境变量 CT_DATA_DIR 可指定其他目录（自测隔离用）。
    /// </summary>
    public static class AppData
    {
        private static readonly string[] DataFiles =
        {
            "ToDoDb.db", "ToDoDb.db-wal", "ToDoDb.db-shm", "ToDoDb.db-journal", "settings.json"
        };

        /// <summary>数据根目录</summary>
        public static string Root { get; }

        public static string DbPath => Path.Combine(Root, "ToDoDb.db");

        public static string SettingsPath => Path.Combine(Root, "settings.json");

        static AppData()
        {
            string legacy = Path.Combine(AppContext.BaseDirectory, "Db");
            string? overrideDir = Environment.GetEnvironmentVariable("CT_DATA_DIR");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Root = !string.IsNullOrWhiteSpace(overrideDir) ? Path.GetFullPath(overrideDir)
                : !string.IsNullOrEmpty(localAppData) ? Path.Combine(localAppData, "CountTimer")
                : legacy;

            try
            {
                MigrateLegacy(legacy, Root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 目标目录不可用（权限/磁盘）：继续用旧位置，保证能启动
                if (!Directory.Exists(Root))
                {
                    Root = legacy;
                }
            }
        }

        /// <summary>
        /// 旧位置有、新位置没有的文件逐个搬过去；目标已存在则不动（中途失败下次启动会继续）。
        /// 先写临时文件再改名，避免拷贝到一半留下残缺的数据库。
        /// </summary>
        internal static void MigrateLegacy(string legacy, string target)
        {
            if (string.Equals(legacy, target, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(legacy))
            {
                return;
            }

            Directory.CreateDirectory(target);
            foreach (string name in DataFiles)
            {
                string source = Path.Combine(legacy, name);
                string destination = Path.Combine(target, name);
                if (!File.Exists(source) || File.Exists(destination))
                {
                    continue;
                }

                string temp = destination + ".tmp";
                File.Copy(source, temp, overwrite: true);
                File.Move(temp, destination);
                try
                {
                    File.Delete(source);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 源文件被占用时留着无妨：下次启动目标已存在，不会再处理
                }
            }
        }
    }
}
