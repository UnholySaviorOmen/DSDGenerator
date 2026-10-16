// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;
using DSDGenerator.Core.Steps;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DSDGenerator.Core.Tests;

public class WriteDsdStepTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _instancePath;
    private readonly WriteDsdStep _step;

    private const string SampleJson = "[{\"original\":\"Iron Sword\",\"string\":\"Железный меч\"}]";

    public WriteDsdStepTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(), "dsdgen-writedsd-" + Guid.NewGuid());
        _instancePath = Path.Combine(_tempDir, "TestInstance");
        Directory.CreateDirectory(_instancePath);

        _step = new WriteDsdStep(NullLogger<WriteDsdStep>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ------------------------------------------------------------------
    //  Хелперы
    // ------------------------------------------------------------------

    private static PluginPair MakePair(
        string originalModName = "SkyUI",
        string pluginFileName = "SkyUI_SE.esp") =>
        new()
        {
            PluginFileName = pluginFileName,
            Original = new PluginFile
            {
                FileName = pluginFileName,
                FullPath = $"/fake/{originalModName}/{pluginFileName}",
            },
            Translated = new PluginFile
            {
                FileName = pluginFileName,
                FullPath = $"/fake/{originalModName} - RU/{pluginFileName}",
            },
            SourceMod = new ModPair
            {
                OriginalName = originalModName,
                RuName = originalModName + " - RU",
                OriginalPath = $"/fake/{originalModName}",
                RuPath = $"/fake/{originalModName} - RU",
            },
        };

    // ------------------------------------------------------------------
    //  Полный путь
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_WritesToExpectedPath()
    {
        var pair = MakePair();

        var path = await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        var expected = Path.Combine(
            _instancePath,
            "__DSD_Output",
            "SkyUI - RU (DSD)",
            "SKSE",
            "Plugins",
            "DynamicStringDistributor",
            "SkyUI_SE.esp",
            "SkyUI_SE.json");

        path.Should().Be(expected);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ContentPreserved()
    {
        var pair = MakePair();

        var path = await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        var written = File.ReadAllText(path);
        written.Should().Be(SampleJson);
    }

    [Fact]
    public async Task Execute_NoBom()
    {
        var pair = MakePair();

        var path = await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        var bytes = File.ReadAllBytes(path);
        var startsWithBom =
            bytes.Length >= 3
            && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

        startsWithBom.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    //  Разные имена плагинов
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SkyUI_SE.esp", "SkyUI_SE.esp", "SkyUI_SE.json")]
    [InlineData("Requiem.esp", "Requiem.esp", "Requiem.json")]
    [InlineData("USSEP.esm", "USSEP.esm", "USSEP.json")]
    [InlineData("Patch.esl", "Patch.esl", "Patch.json")]
    public async Task Execute_VariousPluginNames(
        string pluginFileName,
        string expectedFolder,
        string expectedJsonName)
    {
        var pair = MakePair(pluginFileName: pluginFileName);

        var path = await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        Path.GetFileName(Path.GetDirectoryName(path)!).Should()
            .Be(expectedFolder);
        Path.GetFileName(path).Should().Be(expectedJsonName);
    }

    [Fact]
    public async Task Execute_UpperCaseExtension_KeptInFolderLowercasedInJson()
    {
        // Папка — с расширением как есть ("Plugin.ESP").
        // Файл — stem без расширения + ".json" ("Plugin.json").
        var pair = MakePair(pluginFileName: "Plugin.ESP");

        var path = await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        Path.GetFileName(Path.GetDirectoryName(path)!)
            .Should().Be("Plugin.ESP");
        Path.GetFileName(path).Should().Be("Plugin.json");
    }

    // ------------------------------------------------------------------
    //  Разные имена модов
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SkyUI")]
    [InlineData("Requiem")]
    [InlineData("SkyUI - RU Patched")]  // мод, чьё имя содержит " - RU"
    [InlineData("Mod [Patch] v2")]
    public async Task Execute_VariousOriginalModNames(string originalName)
    {
        var pair = MakePair(originalModName: originalName);

        var path = await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        var outputModName =
            originalName + " - RU (DSD)";

        path.Should().Contain(outputModName);
    }

    // ------------------------------------------------------------------
    //  Создание вложенных папок
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_CreatesNestedDirectories()
    {
        var pair = MakePair();

        // Убедимся, что __DSD_Output/ ещё нет.
        var outputDir = Path.Combine(
            _instancePath, "__DSD_Output");
        Directory.Exists(outputDir).Should().BeFalse();

        await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        Directory.Exists(outputDir).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_OverwritesExistingFile()
    {
        var pair = MakePair();

        await _step.ExecuteAsync(
            _instancePath, pair, "[]", CancellationToken.None);

        var path = await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, CancellationToken.None);

        File.ReadAllText(path).Should().Be(SampleJson);
    }

    // ------------------------------------------------------------------
    //  Ошибки
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Execute_EmptyInstancePath_Throws(string? path)
    {
        var pair = MakePair();

        var act = async () => await _step.ExecuteAsync(
            path!, pair, SampleJson, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_NullPair_Throws()
    {
        var act = async () => await _step.ExecuteAsync(
            _instancePath, null!, SampleJson, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Execute_EmptyJsonContent_Throws(string? json)
    {
        var pair = MakePair();

        var act = async () => await _step.ExecuteAsync(
            _instancePath, pair, json!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_InstanceMissing_Throws()
    {
        var pair = MakePair();
        var missingInstance = Path.Combine(_tempDir, "DoesNotExist");

        var act = async () => await _step.ExecuteAsync(
            missingInstance, pair, SampleJson, CancellationToken.None);

        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task Execute_CanceledToken_Throws()
    {
        var pair = MakePair();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _step.ExecuteAsync(
            _instancePath, pair, SampleJson, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
