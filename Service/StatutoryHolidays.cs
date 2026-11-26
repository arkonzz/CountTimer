using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CountTimer.Share;

namespace CountTimer.Service
{
    /// <summary>
    /// 法定节假日安排（放假与调休）。数据来自开源数据集 NateScarlet/holiday-cn（MIT）：
    /// 程序内置一份兜底，启动后在后台联网更新并缓存到数据目录，网络不通不影响使用。
    /// 每年的安排由国务院年底公布，算不出来，只能靠数据
    /// </summary>
    public sealed class StatutoryHolidays : IDisposable
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(6);
        private const string CacheDirectory = "holidays";
        private const string MarkerFile = "holidays-last-refresh";

        // 依次尝试：国内网络下 GitHub raw 和部分 CDN 常被连接重置，jsDelivr 的 Fastly 节点和 GitHub API 通常可达
        private static readonly string[] Sources =
        {
            "https://fastly.jsdelivr.net/gh/NateScarlet/holiday-cn@master/{0}.json",
            "https://cdn.jsdelivr.net/gh/NateScarlet/holiday-cn@master/{0}.json",
            "https://api.github.com/repos/NateScarlet/holiday-cn/contents/{0}.json?ref=master",
            "https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/{0}.json",
        };

        private static readonly JsonSerializerOptions CacheJson = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>全局实例。必须声明在上面这些静态字段之后：静态字段按声明顺序初始化，构造要用到 RequestTimeout</summary>
        public static readonly StatutoryHolidays Instance = new();

        private readonly object sync = new();
        private readonly Dictionary<int, StatutoryYear?> loaded = new();
        private readonly HttpClient http = new() { Timeout = RequestTimeout };
        private int refreshing;

        /// <summary>数据有更新（后台线程触发，订阅方自行切回界面线程）</summary>
        public event Action? Updated;

        private StatutoryHolidays()
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CountTimer");
        }

        /// <summary>取某一年的安排；没有数据（尚未公布或文件损坏）时为 null</summary>
        public StatutoryYear? Get(int year)
        {
            lock (sync)
            {
                if (loaded.TryGetValue(year, out StatutoryYear? cached))
                {
                    return cached;
                }
            }

            StatutoryYear? data = LoadFile(year) ?? LoadEmbedded(year);
            lock (sync)
            {
                loaded[year] = data;
            }

            return data;
        }

        /// <summary>
        /// 是否工作日：有当年安排时按调休后的结果（周末补班算工作日，工作日放假不算），
        /// 否则按周一至周五
        /// </summary>
        public bool IsWorkday(DateTime date) => StatutoryYear.IsWorkday(date, Get(date.Year));

        /// <summary>后台联网更新 year 和下一年；距上次尝试不足 12 小时则跳过</summary>
        public void RefreshSoon(int year)
        {
            if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    if (DateTime.Now - ReadLastAttempt() < RefreshInterval)
                    {
                        return;
                    }

                    WriteLastAttempt(DateTime.Now);
                    bool changed = false;
                    foreach (int target in new[] { year, year + 1 })
                    {
                        string? json = await FetchAsync(target);
                        if (json is null)
                        {
                            continue;
                        }

                        string path = CachePath(target);
                        string? existing = File.Exists(path) ? File.ReadAllText(path) : null;
                        if (existing == json)
                        {
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        string temp = path + ".tmp";
                        await File.WriteAllTextAsync(temp, json);
                        File.Move(temp, path, overwrite: true);
                        lock (sync)
                        {
                            loaded.Remove(target);
                        }

                        changed = true;
                    }

                    if (changed)
                    {
                        Updated?.Invoke();
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 缓存写不进去：继续用内存里已有的数据
                }
                finally
                {
                    Interlocked.Exchange(ref refreshing, 0);
                }
            });
        }

        private StatutoryYear? LoadFile(int year)
        {
            string path = CachePath(year);
            try
            {
                return File.Exists(path) ? StatutoryYear.Parse(File.ReadAllText(path), year) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static StatutoryYear? LoadEmbedded(int year)
        {
            Assembly assembly = typeof(StatutoryHolidays).Assembly;
            string name = $"{assembly.GetName().Name}.Resources.Holidays.{year}.json";
            using Stream? stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            return StatutoryYear.Parse(reader.ReadToEnd(), year);
        }

        /// <summary>依次尝试各个源，取第一份能通过校验的内容，返回规范化后的缓存文本</summary>
        private async Task<string?> FetchAsync(int year)
        {
            foreach (string source in Sources)
            {
                try
                {
                    using var cancellation = new CancellationTokenSource(RequestTimeout + TimeSpan.FromSeconds(2));
                    string url = string.Format(CultureInfo.InvariantCulture, source, year);
                    string body = await http.GetStringAsync(url, cancellation.Token);
                    string? json = source.Contains("api.github.com", StringComparison.Ordinal) ? UnwrapGitHubApi(body) : body;
                    StatutoryYear? data = json is null ? null : StatutoryYear.Parse(json, year);
                    if (data is null)
                    {
                        continue;
                    }

                    var entries = data.Days.Select(d => new { name = d.Name, date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), isOffDay = d.IsOffDay });
                    return JsonSerializer.Serialize(entries, CacheJson);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                                               or JsonException or IOException or UriFormatException or FormatException)
                {
                }
            }

            return null;
        }

        /// <summary>GitHub API 把文件内容包在 {"encoding":"base64","content":"…"} 里，404 等情况返回的对象没有 content</summary>
        internal static string? UnwrapGitHubApi(string body)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("encoding", out JsonElement encoding)
                    || encoding.GetString() != "base64"
                    || !root.TryGetProperty("content", out JsonElement content))
                {
                    return null;
                }

                return Encoding.UTF8.GetString(Convert.FromBase64String(content.GetString()!.Replace("\n", string.Empty)));
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
            {
                return null;
            }
        }

        private static string CachePath(int year) => Path.Combine(AppData.Root, CacheDirectory, $"{year}.json");

        private static string MarkerPath => Path.Combine(AppData.Root, CacheDirectory, MarkerFile);

        private DateTime ReadLastAttempt()
        {
            try
            {
                return DateTime.TryParseExact(File.ReadAllText(MarkerPath).Trim(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime stamp)
                    ? stamp.ToLocalTime()
                    : DateTime.MinValue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                return DateTime.MinValue;
            }
        }

        private void WriteLastAttempt(DateTime moment)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
                File.WriteAllText(MarkerPath, moment.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        public void Dispose()
        {
            http.Dispose();
        }
    }
}
