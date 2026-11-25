using System.IO;
using CountTimer.Share;

namespace CountTimer.Tests;

/// <summary>用户数据从程序目录迁移到 %LOCALAPPDATA% 的规则</summary>
public class AppDataTests
{
    private static string NewDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "CountTimer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Write(string dir, string name, string content)
    {
        string path = Path.Combine(dir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void MovesDatabaseAndSettingsToNewLocation()
    {
        string legacy = NewDir();
        string target = Path.Combine(NewDir(), "data");
        Write(legacy, "ToDoDb.db", "database-bytes");
        Write(legacy, "settings.json", "{\"FocusMinutes\":20}");

        AppData.MigrateLegacy(legacy, target);

        Assert.Equal("database-bytes", File.ReadAllText(Path.Combine(target, "ToDoDb.db")));
        Assert.Equal("{\"FocusMinutes\":20}", File.ReadAllText(Path.Combine(target, "settings.json")));
        Assert.Empty(Directory.GetFiles(legacy));
        Assert.Empty(Directory.GetFiles(target).Where(f => f.EndsWith(".tmp")));
    }

    [Fact]
    public void DoesNotOverwriteDataAlreadyInNewLocation()
    {
        string legacy = NewDir();
        string target = NewDir();
        Write(target, "settings.json", "current");
        Write(legacy, "settings.json", "stale-leftover");

        AppData.MigrateLegacy(legacy, target);

        Assert.Equal("current", File.ReadAllText(Path.Combine(target, "settings.json")));
    }

    [Fact]
    public void IsNoOpWithoutLegacyDataOrWhenPathsAreTheSame()
    {
        string missing = Path.Combine(NewDir(), "legacy-not-created");
        string target = NewDir();

        AppData.MigrateLegacy(missing, target);
        AppData.MigrateLegacy(target, target);

        Assert.Empty(Directory.GetFiles(target));
    }
}
