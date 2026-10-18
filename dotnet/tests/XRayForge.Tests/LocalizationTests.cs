using XRayForge.Core.Localization;

namespace XRayForge.Tests;

public class LocalizationTests
{
    [Fact]
    public void Tr_translates_and_falls_back_to_key()
    {
        Assert.Equal("Save", LocalizationService.Tr("en", "save"));
        Assert.Equal("Сохранить", LocalizationService.Tr("ru", "save"));
        Assert.Equal("Dialogues", LocalizationService.Tr("en", "tabDialogues"));
        Assert.Equal("unknown_key", LocalizationService.Tr("en", "unknown_key"));
        Assert.Equal("English", LocalizationService.LocaleNames["en"]);
    }

    [Fact]
    public void Theme_and_scan_onboarding_keys_exist_in_both_locales()
    {
        var keys = new[]
        {
            "welcomeSub", "welcomeScanTitle", "welcomeScanSub", "welcomeScanHint",
            "welcomeScanAction", "scanProjectCreated",
            "accentAmber", "accentTeal", "accentSilver",
            "themeSteelSub", "themeOliveSub", "themeHazardSub", "themeAmberSub",
            "themeCobaltSub", "themeArcticSub", "themeGraphiteSub",
            "openBackups", "backupsHint"
        };

        foreach (var key in keys)
        {
            Assert.NotEqual(key, LocalizationService.Tr("ru", key));
            Assert.NotEqual(key, LocalizationService.Tr("en", key));
        }
    }
}
