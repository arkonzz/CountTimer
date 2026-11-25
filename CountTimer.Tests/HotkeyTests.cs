using System.Windows.Forms;
using CountTimer.Model;
using CountTimer.Service;

namespace CountTimer.Tests;

public class HotkeyTests
{
    [Fact]
    public void RoundTripsStoredText()
    {
        Assert.True(Hotkey.TryParse("Ctrl+Alt+Space", out Hotkey hotkey));
        Assert.Equal(Keys.Space, hotkey.Key);
        Assert.Equal((true, true, false), (hotkey.Ctrl, hotkey.Alt, hotkey.Shift));
        Assert.Equal("Ctrl+Alt+Space", hotkey.ToString());
        Assert.True(hotkey.IsValid);
    }

    [Fact]
    public void AcceptsAliasesAndDigitNames()
    {
        Assert.True(Hotkey.TryParse("control + shift + 1", out Hotkey hotkey));
        Assert.Equal("Ctrl+Shift+1", hotkey.ToString());
        Assert.Equal(Keys.F9, Hotkey.TryParse("Ctrl+F9", out Hotkey f9) ? f9.Key : Keys.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Nope")]
    public void RejectsTextWithoutARealKey(string text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void KeyWithoutModifierParsesButIsNotValid()
    {
        Assert.True(Hotkey.TryParse("P", out Hotkey hotkey));
        Assert.False(hotkey.IsValid);
    }

    [Fact]
    public void SplitsModifiersOutOfAKeyEvent()
    {
        Hotkey hotkey = Hotkey.FromKeys(Keys.Control | Keys.Shift | Keys.D5);

        Assert.Equal(new Hotkey(Keys.D5, true, false, true), hotkey);
        Assert.Equal("Ctrl+Shift+5", hotkey.ToString());
        Assert.Equal(Keys.None, Hotkey.FromKeys(Keys.Control | Keys.ControlKey).Key);
        Assert.Equal(Keys.None, Hotkey.FromKeys(Keys.Alt | Keys.Menu).Key);
    }

    [Fact]
    public void SettingsWithBrokenHotkeyDisableItInsteadOfFailingToLoad()
    {
        AppSettings settings = new() { Hotkey = "Ctrl+什么" };

        AppSettingsService.Normalize(settings);

        Assert.Equal(string.Empty, settings.Hotkey);
    }
}
