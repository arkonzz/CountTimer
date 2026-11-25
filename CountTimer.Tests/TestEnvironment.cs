using System.IO;
using System.Runtime.CompilerServices;

namespace CountTimer.Tests;

/// <summary>测试进程使用独立的数据目录，不读写本机真实的用户数据和联网缓存</summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void UseIsolatedDataDirectory()
    {
        Environment.SetEnvironmentVariable("CT_DATA_DIR", Path.Combine(Path.GetTempPath(), "CountTimer.Tests", "data"));
    }
}
