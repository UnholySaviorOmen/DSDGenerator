// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core;
using DSDGenerator.Core.Abstractions;
using DSDGenerator.Core.Models;
using DSDGenerator.Core.Processes;
using DSDGenerator.Core.Progress;
using DSDGenerator.Core.Steps;
using DSDGenerator.Core.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DSDGenerator.Core.Tests;

public class DsdPipelineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _instancePath;
    private readonly string _modsPath;
    private readonly string _fakeExePath;
    private readonly FakeProcessRunner _processRunner;

    public DsdPipelineTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(), "dsdgen-pipeline-" + Guid.NewGuid());
        _instancePath = Path.Combine(_tempDir, "TestInstance");
        _modsPath = Path.Combine(_instancePath, "MO2", "mods");

        Directory.CreateDirectory(_modsPath);

        _fakeExePath = Path.Combine(_tempDir, "esp2dsd.exe");
        File.WriteAllText(_fakeExePath, "fake esp2dsd.exe");

        _processRunner = new FakeProcessRunner();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ------------------------------------------------------------------
    //  Хелперы
    // ------------------------------------------------------------------

    private DsdPipeline BuildPipeline()
    {
        var generator = new Esp2DsdRunner(
            _processRunner,
            _fakeExePath,
            NullLogger<Esp2DsdRunner>.Instance);

        return new DsdPipeline(
            new ScanModsStep(NullLogger<ScanModsStep>.Instance),
            new FindRuPairStep(NullLogger<FindRuPairStep>.Instance),
            new DetectPluginPairStep(NullLogger<DetectPluginPairStep>.Instance),
            generator,
            new WriteDsdStep(NullLogger<WriteDsdStep>.Instance),
            NullLogger<DsdPipeline>.Instance);
    }

    private void CreateMod(string modName, params string[] pluginFileNames)
    {
        var modPath = Path.Combine(_modsPath, modName);
        Directory.CreateDirectory(modPath);

        foreach (var plugin in pluginFileNames)
        {
            File.WriteAllText(
                Path.Combine(modPath, plugin),
                "fake plugin bytes");
        }
    }

    private void SetupSuccessForAllPlugins(
        string jsonContent = "[{\"original\":\"Iron Sword\",\"string\":\"Железный меч\"}]")
    {
        _processRunner.OnRun = (_, args, wd) =>
        {
            // Имя выходного файла зависит от оригинального плагина.
            var originalPluginPath = args[0];
            var stem = Path.GetFileNameWithoutExtension(originalPluginPath);
            var suffix = Path.GetExtension(originalPluginPath);
            var outputFileName = $"{stem}_output{suffix}.json";

            var outputDir = Path.Combine(wd, "Output");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(
                Path.Combine(outputDir, outputFileName),
                jsonContent);

            return new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "ok",
                StandardError = "",
            };
        };
    }

    private string OutputDsdPath(
        string originalModName,
        string pluginFileName)
    {
        var pluginStem = Path.GetFileNameWithoutExtension(pluginFileName);
        return Path.Combine(
            _instancePath,
            "__DSD_Output",
            originalModName + " - RU (DSD)",
            "SKSE",
            "Plugins",
            "DynamicStringDistributor",
            pluginFileName,
            pluginStem + ".json");
    }

    // ------------------------------------------------------------------
    //  Валидация аргументов
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Execute_EmptyInstancePath_Throws(string? path)
    {
        var pipeline = BuildPipeline();

        var act = async () => await pipeline.ExecuteAsync(
            path!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ------------------------------------------------------------------
    //  Пустой инстанс
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_NoMods_ReturnsEmptySummary()
    {
        var pipeline = BuildPipeline();

        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.ModsScanned.Should().Be(0);
        summary.ModPairsFound.Should().Be(0);
        summary.PluginPairsFound.Should().Be(0);
        summary.Generated.Should().Be(0);
        summary.Skipped.Should().Be(0);
        summary.Failed.Should().Be(0);
        summary.Results.Should().BeEmpty();

        // __DSD_Output/ создаётся даже при пустом прогоне.
        Directory.Exists(summary.OutputPath).Should().BeTrue();
    }

    // ------------------------------------------------------------------
    //  Мод без пары
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_OriginalWithoutRu_NoPluginPairs()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.ModsScanned.Should().Be(1);
        summary.ModPairsFound.Should().Be(0);
        summary.PluginPairsFound.Should().Be(0);
        summary.Generated.Should().Be(0);
        summary.Skipped.Should().Be(0);
    }

    // ------------------------------------------------------------------
    //  Happy path: одна пара
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_OneSimplePair_GeneratesFile()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.ModsScanned.Should().Be(2);
        summary.ModPairsFound.Should().Be(1);
        summary.PluginPairsFound.Should().Be(1);
        summary.Generated.Should().Be(1);
        summary.Skipped.Should().Be(0);
        summary.Failed.Should().Be(0);

        var outputPath = OutputDsdPath("SkyUI", "SkyUI_SE.esp");
        File.Exists(outputPath).Should().BeTrue();
    }

    // ------------------------------------------------------------------
    //  Несколько пар, несколько плагинов
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_MultiplePairsAndPlugins_AllGenerated()
    {
        CreateMod("A", "Main.esp", "Patch.esp");
        CreateMod("A - RU", "Main.esp", "Patch.esp");
        CreateMod("B", "Only.esp");
        CreateMod("B - RU", "Only.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.ModPairsFound.Should().Be(2);
        summary.PluginPairsFound.Should().Be(3);
        summary.Generated.Should().Be(3);
        summary.Failed.Should().Be(0);
        summary.Skipped.Should().Be(0);

        File.Exists(OutputDsdPath("A", "Main.esp")).Should().BeTrue();
        File.Exists(OutputDsdPath("A", "Patch.esp")).Should().BeTrue();
        File.Exists(OutputDsdPath("B", "Only.esp")).Should().BeTrue();
    }

    // ------------------------------------------------------------------
    //  Skip-сценарии из DetectPluginPair
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_OriginalWithoutPlugins_SkipsMod()
    {
        // Мод без плагинов — но с парой.
        CreateMod("Resource", "readme.txt");
        CreateMod("Resource - RU", "SomePlugin.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.ModPairsFound.Should().Be(1);
        summary.PluginPairsFound.Should().Be(0);
        summary.Generated.Should().Be(0);
        summary.Skipped.Should().Be(1);
        summary.Results[0].Message.Should().Be(
            PluginSkipReasons.NoPluginsInOriginal);
    }

    [Fact]
    public async Task Execute_RuWithoutPlugins_SkipsAllOriginalPlugins()
    {
        CreateMod("Mod", "A.esp", "B.esp");
        CreateMod("Mod - RU"); // без плагинов
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.Generated.Should().Be(0);
        summary.Skipped.Should().Be(2);
        summary.Results.Should().AllSatisfy(r =>
            r.Message.Should().Be(PluginSkipReasons.NoPluginsInRu));
    }

    [Fact]
    public async Task Execute_PluginWithoutPairInRu_SkipsOnlyThatPlugin()
    {
        CreateMod("Mod", "Main.esp", "OnlyInOriginal.esp");
        CreateMod("Mod - RU", "Main.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.PluginPairsFound.Should().Be(1);
        summary.Generated.Should().Be(1);
        summary.Skipped.Should().Be(1);
        summary.Results.Should().Contain(r =>
            r.PluginFileName == "OnlyInOriginal.esp" &&
            r.Kind == DsdResultKind.Skipped);
    }

    // ------------------------------------------------------------------
    //  Ошибка esp2dsd для одного плагина → Failed, остальные OK
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_GeneratorFailsForOnePlugin_OthersSucceed()
    {
        CreateMod("A", "Main.esp", "Patch.esp");
        CreateMod("A - RU", "Main.esp", "Patch.esp");

        // Первый вызов успешен, второй — падает с exit code.
        var callCount = 0;
        _processRunner.OnRun = (_, args, wd) =>
        {
            callCount++;
            if (callCount == 2)
            {
                return new ProcessRunResult
                {
                    ExitCode = 1,
                    StandardOutput = "",
                    StandardError = "boom",
                };
            }

            var originalPluginPath = args[0];
            var stem = Path.GetFileNameWithoutExtension(originalPluginPath);
            var suffix = Path.GetExtension(originalPluginPath);
            var outputFileName = $"{stem}_output{suffix}.json";

            var outputDir = Path.Combine(wd, "Output");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(
                Path.Combine(outputDir, outputFileName),
                "[{\"test\":\"data\"}]");

            return new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = "",
                StandardError = "",
            };
        };

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.PluginPairsFound.Should().Be(2);
        summary.Generated.Should().Be(1);
        summary.Failed.Should().Be(1);
        summary.Skipped.Should().Be(0);
    }

    [Fact]
    public async Task Execute_AllGeneratorsFail_ReturnsAllFailed()
    {
        CreateMod("A", "Main.esp");
        CreateMod("A - RU", "Main.esp");

        _processRunner.OnRun = (_, _, _) => new ProcessRunResult
        {
            ExitCode = 2,
            StandardOutput = "",
            StandardError = "always fails",
        };

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.Generated.Should().Be(0);
        summary.Failed.Should().Be(1);
        summary.Results[0].Message.Should().Contain("exit code 2");
    }

    // ------------------------------------------------------------------
    //  Очистка __DSD_Output/ перед прогоном
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_CleansOutputDirBeforeRun()
    {
        // Старый мусор в __DSD_Output/.
        var outputDir = Path.Combine(_instancePath, "__DSD_Output");
        Directory.CreateDirectory(outputDir);

        var staleDir = Path.Combine(outputDir, "OldMod - RU (DSD)");
        Directory.CreateDirectory(staleDir);
        File.WriteAllText(
            Path.Combine(staleDir, "stale.json"), "junk");

        var staleFile = Path.Combine(outputDir, "stale-root.txt");
        File.WriteAllText(staleFile, "junk");

        // Мод с парой — чтобы pipeline что-то сгенерил.
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        Directory.Exists(staleDir).Should().BeFalse();
        File.Exists(staleFile).Should().BeFalse();

        // Новое сгенерилось.
        File.Exists(OutputDsdPath("SkyUI", "SkyUI_SE.esp")).Should().BeTrue();
    }

    // ------------------------------------------------------------------
    //  Прогресс
    // ------------------------------------------------------------------

    private sealed class ListProgress : IProgress<StepProgress>
    {
        public List<StepProgress> Reports { get; } = new();
        public void Report(StepProgress value) => Reports.Add(value);
    }

    [Fact]
    public async Task Execute_WithProgress_ReportsAllSteps()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        SetupSuccessForAllPlugins();

        var progress = new ListProgress();
        var pipeline = BuildPipeline();

        await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None, progress);

        progress.Reports.Should().NotBeEmpty();
        progress.Reports.Should().OnlyContain(r => r.TotalSteps == 4);

        var distinctIndices = progress.Reports
            .Select(r => r.StepIndex)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        distinctIndices.Should().Equal(1, 2, 3, 4);

        // Финальный шаг — GenerateDsd.
        progress.Reports[^1].StepName.Should().Be("GenerateDsd");
    }

    // ------------------------------------------------------------------
    //  Отмена
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_Canceled_Throws()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        SetupSuccessForAllPlugins();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pipeline = BuildPipeline();

        var act = async () => await pipeline.ExecuteAsync(
            _instancePath, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------
    //  Сводка
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_SummaryContainsInstanceAndOutputPath()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.InstancePath.Should().Be(Path.GetFullPath(_instancePath));
        summary.OutputPath.Should().Be(
            Path.Combine(Path.GetFullPath(_instancePath), "__DSD_Output"));
    }

    [Fact]
    public async Task Execute_OrphanRuModsCounted()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        CreateMod("OrphanMod - RU", "whatever.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.OrphanRuMods.Should().Be(1);
    }

    // ------------------------------------------------------------------
    //  OrphanOriginals
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_OrphanOriginalsCounted()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        CreateMod("NoTranslation");
        CreateMod("AnotherOrphan");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.OrphanOriginals.Should().Be(2);
        summary.OrphanRuMods.Should().Be(0);
    }

    [Fact]
    public async Task Execute_NoOrphans_CountersZero()
    {
        CreateMod("SkyUI", "SkyUI_SE.esp");
        CreateMod("SkyUI - RU", "SkyUI_SE.esp");
        SetupSuccessForAllPlugins();

        var pipeline = BuildPipeline();
        var summary = await pipeline.ExecuteAsync(
            _instancePath, CancellationToken.None);

        summary.OrphanOriginals.Should().Be(0);
        summary.OrphanRuMods.Should().Be(0);
    }
}
