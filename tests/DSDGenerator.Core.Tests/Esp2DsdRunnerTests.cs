// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Abstractions;
using DSDGenerator.Core.Models;
using DSDGenerator.Core.Processes;
using DSDGenerator.Core.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DSDGenerator.Core.Tests;

public class Esp2DsdRunnerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _fakeExePath;
    private readonly string _originalPluginPath;
    private readonly string _translatedPluginPath;
    private readonly FakeProcessRunner _processRunner;
    private readonly Esp2DsdRunner _runner;

    public Esp2DsdRunnerTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(), "dsdgen-runner-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        // Путь к fake exe — в нашей temp-папке, а не в
        // AppContext.BaseDirectory/Assets/. Это делает тесты
        // полностью независимыми от глобального состояния.
        _fakeExePath = Path.Combine(_tempDir, "esp2dsd.exe");
        File.WriteAllText(_fakeExePath, "fake esp2dsd.exe for tests");

        _originalPluginPath = Path.Combine(_tempDir, "SkyUI_SE.esp");
        _translatedPluginPath = Path.Combine(_tempDir, "SkyUI_SE_ru.esp");
        File.WriteAllText(_originalPluginPath, "fake original plugin");
        File.WriteAllText(_translatedPluginPath, "fake translated plugin");

        _processRunner = new FakeProcessRunner();
        _runner = new Esp2DsdRunner(
            _processRunner,
            _fakeExePath,
            NullLogger<Esp2DsdRunner>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ------------------------------------------------------------------
    //  Хелперы
    // ------------------------------------------------------------------

    private static PluginPair MakePair(
        string originalPath,
        string translatedPath,
        string pluginFileName = "SkyUI_SE.esp") =>
        new()
        {
            PluginFileName = pluginFileName,
            Original = new PluginFile
            {
                FileName = pluginFileName,
                FullPath = originalPath,
            },
            Translated = new PluginFile
            {
                FileName = pluginFileName,
                FullPath = translatedPath,
            },
            SourceMod = new ModPair
            {
                OriginalName = "SkyUI",
                RuName = "SkyUI - RU",
                OriginalPath = "/fake/SkyUI",
                RuPath = "/fake/SkyUI - RU",
            },
        };

    /// <summary>
    /// Готовит fake-поведение: exe успешно создаёт ожидаемый
    /// выходной файл в рабочей директории.
    /// </summary>
    private void SetupSuccess(
        string pluginFileName = "SkyUI_SE.esp",
        string jsonContent = "[{\"test\":\"data\"}]")
    {
        var outputFileName =
            Esp2DsdRunner.BuildOutputFileName(pluginFileName);

        _processRunner.OnRun = (_, _, wd) =>
        {
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

    // ------------------------------------------------------------------
    //  BuildOutputFileName
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SkyUI_SE.esp", "SkyUI_SE_output.esp.json")]
    [InlineData("SkyUI.esm", "SkyUI_output.esm.json")]
    [InlineData("Patch.esl", "Patch_output.esl.json")]
    [InlineData("Plugin.ESP", "Plugin_output.ESP.json")]
    public void BuildOutputFileName_FormatsCorrectly(
        string input, string expected)
    {
        Esp2DsdRunner.BuildOutputFileName(input)
            .Should().Be(expected);
    }

    // ------------------------------------------------------------------
    //  esp2dsd.exe отсутствует
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExeMissing_Throws()
    {
        // Удаляем fake exe — этот тест проверяет отсутствие.
        File.Delete(_fakeExePath);

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*esp2dsd.exe not found*")
            .WithMessage("*Assets*");

        _processRunner.CallCount.Should().Be(0);
    }

    // ------------------------------------------------------------------
    //  Оригинал / перевод не существуют
    // ------------------------------------------------------------------

    [Fact]
    public async Task OriginalPluginMissing_Throws()
    {
        var pair = MakePair(
            Path.Combine(_tempDir, "Missing.esp"),
            _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*Original plugin not found*");

        _processRunner.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task TranslatedPluginMissing_Throws()
    {
        var pair = MakePair(
            _originalPluginPath,
            Path.Combine(_tempDir, "Missing.esp"));

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("*Translated plugin not found*");

        _processRunner.CallCount.Should().Be(0);
    }

    // ------------------------------------------------------------------
    //  Успешный сценарий
    // ------------------------------------------------------------------

    [Fact]
    public async Task Success_ReturnsJsonContent()
    {
        SetupSuccess();

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var result = await _runner.GenerateAsync(
            pair, CancellationToken.None);

        result.Should().Be("[{\"test\":\"data\"}]");
        _processRunner.CallCount.Should().Be(1);
        _processRunner.LastFileName.Should().Be(_fakeExePath);
        _processRunner.LastArguments.Should().HaveCount(2);
        _processRunner.LastArguments![0].Should().Be(_originalPluginPath);
        _processRunner.LastArguments[1].Should().Be(_translatedPluginPath);
    }

    [Fact]
    public async Task Success_TempDirectoryCleanedUp()
    {
        SetupSuccess();

        string? observedTempDir = null;
        var originalOnRun = _processRunner.OnRun!;
        _processRunner.OnRun = (fn, args, wd) =>
        {
            observedTempDir = wd;
            return originalOnRun(fn, args, wd);
        };

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);
        await _runner.GenerateAsync(pair, CancellationToken.None);

        observedTempDir.Should().NotBeNull();
        Directory.Exists(observedTempDir!).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    //  Exit code ≠ 0
    // ------------------------------------------------------------------

    [Fact]
    public async Task NonZeroExitCode_Throws()
    {
        _processRunner.OnRun = (_, _, _) => new ProcessRunResult
        {
            ExitCode = 2,
            StandardOutput = "",
            StandardError = "something failed",
        };

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exit code 2*")
            .WithMessage("*something failed*");
    }

    // ------------------------------------------------------------------
    //  Выходной файл не создан
    // ------------------------------------------------------------------

    [Fact]
    public async Task OutputFileMissing_Throws()
    {
        _processRunner.OnRun = (_, _, _) => new ProcessRunResult
        {
            ExitCode = 0,
            StandardOutput = "ok",
            StandardError = "",
        };

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*did not produce expected output*");
    }

    // ------------------------------------------------------------------
    //  Битый / невалидный JSON
    // ------------------------------------------------------------------

    [Fact]
    public async Task MalformedJson_Throws()
    {
        SetupSuccess(jsonContent: "{ not valid json }");

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*malformed JSON*");
    }

    [Fact]
    public async Task EmptyJson_Throws()
    {
        SetupSuccess(jsonContent: "");

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty output*");
    }

    [Fact]
    public async Task NonArrayJson_Throws()
    {
        SetupSuccess(jsonContent: "{\"foo\":\"bar\"}");

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*non-array*");
    }

    [Fact]
    public async Task EmptyArrayJson_Throws()
    {
        SetupSuccess(jsonContent: "[]");

        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        var act = async () => await _runner.GenerateAsync(
            pair, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty JSON array*");
    }

    // ------------------------------------------------------------------
    //  Отмена
    // ------------------------------------------------------------------

    [Fact]
    public async Task CanceledToken_Throws()
    {
        var pair = MakePair(_originalPluginPath, _translatedPluginPath);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _runner.GenerateAsync(pair, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------
    //  Валидация аргументов
    // ------------------------------------------------------------------

    [Fact]
    public async Task NullPair_Throws()
    {
        var act = async () => await _runner.GenerateAsync(
            null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
