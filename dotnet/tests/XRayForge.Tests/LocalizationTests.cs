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
    public void Dialogue_game_export_keys_exist_in_both_locales()
    {
        var keys = new[]
        {
            "gameExportDialogs", "gameExportDone", "gameExportEmpty",
            "gameExportOverwrite", "gameExportFailed"
        };

        foreach (var key in keys)
        {
            Assert.NotEqual(key, LocalizationService.Tr("ru", key));
            Assert.NotEqual(key, LocalizationService.Tr("en", key));
        }
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

    [Fact]
    public void Stage7_tabs_and_stage8_keys_exist_in_both_locales()
    {
        var keys = new[]
        {
            "tabSystemHint", "systemSubtitle", "systemAddInclude", "systemIncludeAdded",
            "systemIncludeFixed", "systemDownload", "systemDownloaded", "systemDownloadFailed",
            "systemIncludeTitle", "systemBroken", "systemNoIncludes", "systemIncludeFound",
            "systemIncludeMissing", "systemIncludeFix", "systemIncludeComment", "systemIncludeTree",
            "systemSections", "systemSectionAdd", "systemSectionName", "systemSectionCreated",
            "systemNoSections", "systemAddEntry", "systemRemoveEntry", "systemSectionsLoaded",
            "systemSearchSection", "systemNoLoadedSections", "systemChecks", "systemNoIssues",
            "systemLtxFiles", "systemDeleteFile", "systemFileDeleted", "systemLoadSpawn",
            "systemSpawn", "systemSpawnFailed", "systemScriptMissing",
            "tabLanguagesHint", "languagesSubtitle", "languagesSummaryMode", "languagesByLocale",
            "languagesIssues", "languagesStats", "languagesTables", "languagesCoverage",
            "languagesTableCreated", "languagesDeleteTable", "languagesTableDeleted",
            "languagesImport", "languagesImportEmpty", "languagesImported", "languagesEntryAdded",
            "languagesEntryRemoved", "languagesAddEntry", "languagesLocale", "languagesName",
            "languagesUsage", "languagesUsedIn", "languagesUnused", "languagesEmpty",
            "languagesSummary", "languagesNoRows", "languagesId",
            "tabQuestsHint", "questsSubtitle", "questsTotal", "questsCop", "questsChecks",
            "questsRelated", "questsDiff", "questsChains", "questsPreview", "questsCopFormat",
            "questsSocFormat", "questsEditor", "questsNoMatches", "questsEmpty", "questsPriority",
            "questsTitleField", "questsTextField", "questsComplete", "questsFail", "questsIcon",
            "questsLocation", "questsArticle", "questsObjectives", "questsObjectivesShort",
            "questsDuplicate", "questsMatches", "questsAddObjective", "questsAddCondition",
            "questsCreated", "questsDeleted", "questsNoTranslationIds", "questsBound",
            "questsExportXml", "questsExportLtx", "questsExported", "questsExportFailed",
            "questsNoTaskManager", "questsCreate", "questsTemplate", "questsSearch",
            "questsSortName", "questsSortPrior", "questsCategory", "questsTitle", "questsText",
            "questsRequires", "questsReward", "questsNoObjectives", "questsNoConditions",
            "questsBind", "questsDeleteQuest", "questsPreviewLocale", "questsStoryline",
            "questsRewardMoney", "questsRewardItem", "questsRewardTreasure", "questsCondlist",
            "questsCompleteInfo", "questsFailInfo", "questsNoRelated", "questsDependencies",
            "questsChainsEmpty", "exportGameZip", "gameZipExported", "selfCheck", "selfCheckHint"
        };

        foreach (var key in keys)
        {
            Assert.NotEqual(key, LocalizationService.Tr("ru", key));
            Assert.NotEqual(key, LocalizationService.Tr("en", key));
        }
    }

    [Fact]
    public void Scripts_tab_keys_exist_in_both_locales()
    {
        var keys = new[]
        {
            "tabScriptsHint", "scriptApi", "scriptCreate", "scriptOpen", "scriptScan",
            "scriptSearch", "scriptSortByName", "scriptReplaceWith", "scriptReplaceAll",
            "scriptSnippets", "scriptInsertApi", "scriptInsertSnippet", "scriptRename",
            "scriptDelete", "scriptDownload", "scriptFormat", "scriptDiff", "scriptEdit",
            "scriptView", "scriptEditorEmpty", "scriptEmpty", "scriptNoMatches",
            "scriptFiles", "scriptFound", "scriptLinesShort", "scriptModified",
            "scriptLine", "scriptBrackets", "scriptCheckLua", "scriptHideDiagnostics",
            "scriptDiagnosticsToggle", "scriptCallGraph",
            "scriptFunctions", "scriptFunctionsShort", "scriptCalls", "scriptJumpDefinition",
            "scriptDefinition", "scriptDefinitionMissing", "scriptSource", "scriptCurrent",
            "scriptUnused", "scriptDuplicateFunction", "scriptCollapsedLines",
            "scriptCreated", "scriptRenamed", "scriptDeleted", "scriptNameExists",
            "scriptFileName", "scriptNothingReplaced", "scriptReplaced", "scriptFormatted",
            "scriptInserted", "scriptAlreadyAdded", "scriptOpened", "scriptOpenFailed",
            "scriptScanned", "scriptSaved", "scriptSaveFailed"
        };

        foreach (var key in keys)
        {
            Assert.NotEqual(key, LocalizationService.Tr("ru", key));
            Assert.NotEqual(key, LocalizationService.Tr("en", key));
        }
    }
}
