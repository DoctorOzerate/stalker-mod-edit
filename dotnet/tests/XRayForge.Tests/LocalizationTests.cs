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
}
