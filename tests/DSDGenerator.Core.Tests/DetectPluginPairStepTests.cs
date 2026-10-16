// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;
using DSDGenerator.Core.Steps;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DSDGenerator.Core.Tests;

public class DetectPluginPairStepTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DetectPluginPairStep _step;

    public DetectPluginPairStepTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(), "dsdgen-detectplugins-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        _step = new DetectPluginPairStep(
            NullLogger<DetectPluginPairStep>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ------------------------------------------------------------------
    //  Хелперы
    // ------------------------------------------------------------------

    private string MakeModDir(string name)
    {
        var path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private void WritePlugin(string modPath, string fileName)
    {
        File.WriteAllText(Path.Combine(modPath, fileName), "fake plugin data");
    }

    private ModPair MakeModPair(
        string originalName,
        string ruName,
        string? originalPath = null,
        string? ruPath = null)
    {
        return new ModPair
        {
            OriginalName = originalName,
            RuName = ruName,
            OriginalPath = originalPath ?? Path.Combine(_tempDir, originalName),
            RuPath = ruPath ?? Path.Combine(_tempDir, ruName),
        };
    }

    // ------------------------------------------------------------------
    //  Пустой список
    // ------------------------------------------------------------------

    [Fact]
    public async Task EmptyModPairs_ReturnsEmpty()
    {
        var result = await _step.ExecuteAsync(
            Array.Empty<ModPair>(), CancellationToken.None);

        result.Pairs.Should().BeEmpty();
        result.Skipped.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    //  Одна простая пара
    // ------------------------------------------------------------------

    [Fact]
    public async Task SinglePluginPair_Found()
    {
        var original = MakeModDir("SkyUI");
        var ru = MakeModDir("SkyUI - RU");
        WritePlugin(original, "SkyUI_SE.esp");
        WritePlugin(ru, "SkyUI_SE.esp");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("SkyUI", "SkyUI - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(1);
        var pair = result.Pairs[0];
        pair.PluginFileName.Should().Be("SkyUI_SE.esp");
        pair.Original.FileName.Should().Be("SkyUI_SE.esp");
        pair.Original.FullPath.Should().Be(
            Path.Combine(original, "SkyUI_SE.esp"));
        pair.Translated.FileName.Should().Be("SkyUI_SE.esp");
        pair.Translated.FullPath.Should().Be(
            Path.Combine(ru, "SkyUI_SE.esp"));
        pair.SourceMod.OriginalName.Should().Be("SkyUI");

        result.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiplePluginPairs_AllFound()
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, "Main.esp");
        WritePlugin(original, "Patch.esp");
        WritePlugin(ru, "Main.esp");
        WritePlugin(ru, "Patch.esp");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(2);
        result.Pairs.Select(p => p.PluginFileName)
            .Should().Equal("Main.esp", "Patch.esp");
        result.Skipped.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    //  Skip: оригинал без плагинов
    // ------------------------------------------------------------------

    [Fact]
    public async Task OriginalWithNoPlugins_SkipWholeMod()
    {
        var original = MakeModDir("ResourceMod");
        var ru = MakeModDir("ResourceMod - RU");
        // Никаких .esp/.esm/.esl — только текстовые файлы.
        File.WriteAllText(Path.Combine(original, "readme.txt"), "junk");
        WritePlugin(ru, "Whatever.esp");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("ResourceMod", "ResourceMod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].PluginFileName.Should().Be("");
        result.Skipped[0].SourceModName.Should().Be("ResourceMod");
        result.Skipped[0].Reason.Should().Be(
            PluginSkipReasons.NoPluginsInOriginal);
    }

    // ------------------------------------------------------------------
    //  Skip: RU без плагинов
    // ------------------------------------------------------------------

    [Fact]
    public async Task RuWithNoPlugins_SkipAllOriginalPlugins()
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, "A.esp");
        WritePlugin(original, "B.esp");
        // В RU ничего нет.

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().BeEmpty();
        result.Skipped.Should().HaveCount(2);
        result.Skipped.Select(s => s.PluginFileName)
            .Should().Equal("A.esp", "B.esp");
        result.Skipped.Should().AllSatisfy(s =>
            s.Reason.Should().Be(PluginSkipReasons.NoPluginsInRu));
    }

    // ------------------------------------------------------------------
    //  Skip: плагин без пары в RU
    // ------------------------------------------------------------------

    [Fact]
    public async Task PluginWithoutMatchInRu_Skipped()
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, "Main.esp");
        WritePlugin(original, "OnlyInOriginal.esp");
        WritePlugin(ru, "Main.esp");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(1);
        result.Pairs[0].PluginFileName.Should().Be("Main.esp");

        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].PluginFileName.Should().Be("OnlyInOriginal.esp");
        result.Skipped[0].Reason.Should().Be(
            PluginSkipReasons.NoMatchingTranslatedPlugin);
    }

    [Fact]
    public async Task PluginOnlyInRu_Ignored()
    {
        // Плагин есть в RU, но нет в оригинале — просто игнорируем.
        // Ни в Pairs, ни в Skipped.
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, "Main.esp");
        WritePlugin(ru, "Main.esp");
        WritePlugin(ru, "Orphan.esp");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(1);
        result.Skipped.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    //  Расширения
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Plugin.esp")]
    [InlineData("Plugin.esm")]
    [InlineData("Plugin.esl")]
    public async Task AllPluginExtensions_Recognized(string fileName)
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, fileName);
        WritePlugin(ru, fileName);

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(1);
        result.Pairs[0].PluginFileName.Should().Be(fileName);
    }

    [Fact]
    public async Task NonPluginFiles_Ignored()
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        File.WriteAllText(Path.Combine(original, "readme.txt"), "junk");
        File.WriteAllText(Path.Combine(original, "SkyUI.bsa"), "junk");
        File.WriteAllText(Path.Combine(original, "SkyUI.ba2"), "junk");
        File.WriteAllText(Path.Combine(ru, "readme.txt"), "junk");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        // .esp/.esm/.esl нет — мод skip с NoPluginsInOriginal.
        result.Pairs.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].Reason.Should().Be(
            PluginSkipReasons.NoPluginsInOriginal);
    }

    // ------------------------------------------------------------------
    //  Регистр расширения
    // ------------------------------------------------------------------

    [Fact]
    public async Task UppercaseExtension_Recognized()
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, "Plugin.ESP");
        WritePlugin(ru, "Plugin.ESP");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(1);
        result.Pairs[0].PluginFileName.Should().Be("Plugin.ESP");
    }

    [Fact]
    public async Task MixedCaseExtension_MatchByFileNameCaseInsensitive()
    {
        // Windows ФС — регистронезависима. Плагин "Plugin.ESP"
        // и "plugin.esp" — разные записи в ФС (не могут
        // сосуществовать), но если по какой-то причине имена
        // различаются регистром между модами, всё равно матчим.
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, "SkyUI.esp");
        WritePlugin(ru, "skyui.ESP");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(1);
        result.Pairs[0].PluginFileName.Should().Be("SkyUI.esp");
    }

    // ------------------------------------------------------------------
    //  Подпапки игнорируются
    // ------------------------------------------------------------------

    [Fact]
    public async Task PluginInSubdir_Ignored()
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");

        Directory.CreateDirectory(Path.Combine(original, "Data"));
        Directory.CreateDirectory(Path.Combine(ru, "Data"));
        File.WriteAllText(
            Path.Combine(original, "Data", "InSubdir.esp"), "junk");
        File.WriteAllText(
            Path.Combine(ru, "Data", "InSubdir.esp"), "junk");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].Reason.Should().Be(
            PluginSkipReasons.NoPluginsInOriginal);
    }

    // ------------------------------------------------------------------
    //  Мод не существует (удалён между шагами)
    // ------------------------------------------------------------------

    [Fact]
    public async Task OriginalModMissing_TreatedAsNoPlugins()
    {
        var ru = MakeModDir("Mod - RU");
        WritePlugin(ru, "Plugin.esp");

        var result = await _step.ExecuteAsync(
            new[]
            {
                MakeModPair(
                    "Mod", "Mod - RU",
                    originalPath: Path.Combine(_tempDir, "DoesNotExist"),
                    ruPath: ru),
            },
            CancellationToken.None);

        result.Pairs.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].Reason.Should().Be(
            PluginSkipReasons.NoPluginsInOriginal);
    }

    // ------------------------------------------------------------------
    //  Несколько модов
    // ------------------------------------------------------------------

    [Fact]
    public async Task MultipleMods_OrderPreserved()
    {
        var a = MakeModDir("A");
        var aRu = MakeModDir("A - RU");
        var b = MakeModDir("B");
        var bRu = MakeModDir("B - RU");

        WritePlugin(a, "A.esp");
        WritePlugin(aRu, "A.esp");
        WritePlugin(b, "B.esp");
        WritePlugin(bRu, "B.esp");

        var result = await _step.ExecuteAsync(
            new[]
            {
                MakeModPair("A", "A - RU", a, aRu),
                MakeModPair("B", "B - RU", b, bRu),
            },
            CancellationToken.None);

        result.Pairs.Should().HaveCount(2);
        result.Pairs.Select(p => p.SourceMod.OriginalName)
            .Should().Equal("A", "B");
    }

    // ------------------------------------------------------------------
    //  Порядок плагинов
    // ------------------------------------------------------------------

    [Fact]
    public async Task PluginsSortedOrdinal()
    {
        var original = MakeModDir("Mod");
        var ru = MakeModDir("Mod - RU");
        WritePlugin(original, "Zeta.esp");
        WritePlugin(original, "alpha.esp");
        WritePlugin(original, "Beta.esp");
        WritePlugin(ru, "Zeta.esp");
        WritePlugin(ru, "alpha.esp");
        WritePlugin(ru, "Beta.esp");

        var result = await _step.ExecuteAsync(
            new[] { MakeModPair("Mod", "Mod - RU", original, ru) },
            CancellationToken.None);

        result.Pairs.Select(p => p.PluginFileName).Should().Equal(
            "Beta.esp", "Zeta.esp", "alpha.esp");
    }

    // ------------------------------------------------------------------
    //  Отмена и валидация
    // ------------------------------------------------------------------

    [Fact]
    public async Task CanceledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _step.ExecuteAsync(
            Array.Empty<ModPair>(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task NullModPairs_Throws()
    {
        var act = async () => await _step.ExecuteAsync(
            null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
