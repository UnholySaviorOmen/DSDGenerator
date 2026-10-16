// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Steps;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DSDGenerator.Core.Tests;

public class ScanModsStepTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _instancePath;
    private readonly string _modsPath;
    private readonly ScanModsStep _step;

    public ScanModsStepTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(), "dsdgen-scanmods-" + Guid.NewGuid());
        _instancePath = Path.Combine(_tempDir, "TestInstance");
        _modsPath = Path.Combine(_instancePath, "MO2", "mods");

        Directory.CreateDirectory(_modsPath);

        _step = new ScanModsStep(
            NullLogger<ScanModsStep>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private void CreateMod(string name)
    {
        Directory.CreateDirectory(Path.Combine(_modsPath, name));
    }

    // ------------------------------------------------------------------
    //  Валидация аргументов
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Execute_EmptyPath_Throws(string? path)
    {
        var act = async () => await _step.ExecuteAsync(
            path!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_InstanceMissing_Throws()
    {
        var missing = Path.Combine(_tempDir, "DoesNotExist");

        var act = async () => await _step.ExecuteAsync(
            missing, CancellationToken.None);

        await act.Should().ThrowAsync<DirectoryNotFoundException>()
            .WithMessage("*Instance directory not found*");
    }

    [Fact]
    public async Task Execute_Mo2Missing_Throws()
    {
        Directory.Delete(Path.Combine(_instancePath, "MO2"), recursive: true);

        var act = async () => await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        await act.Should().ThrowAsync<DirectoryNotFoundException>()
            .WithMessage("*MO2/*")
            .WithMessage("*MO2 instance*");
    }

    [Fact]
    public async Task Execute_ModsMissing_Throws()
    {
        Directory.Delete(_modsPath, recursive: true);

        var act = async () => await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        await act.Should().ThrowAsync<DirectoryNotFoundException>()
            .WithMessage("*MO2/mods/*");
    }

    // ------------------------------------------------------------------
    //  Базовое поведение
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_EmptyMods_ReturnsEmpty()
    {
        var result = await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_OneMod_ReturnsOne()
    {
        CreateMod("SkyUI");

        var result = await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("SkyUI");
        result[0].FullPath.Should().Be(Path.Combine(_modsPath, "SkyUI"));
    }

    [Fact]
    public async Task Execute_MultipleMods_ReturnsAll()
    {
        CreateMod("SkyUI");
        CreateMod("Requiem");
        CreateMod("USSEP");

        var result = await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(m => m.Name).Should().Equal(
            "Requiem", "SkyUI", "USSEP");
    }

    [Fact]
    public async Task Execute_SortedOrdinal()
    {
        // Windows-файловая система регистронезависима: "alpha" и "ALPHA"
        // — это одна папка. Проверяем Ordinal-сортировку через имена,
        // различающиеся по первой букве (регистр имеет значение).
        CreateMod("Zeta");   // Z = 0x5A
        CreateMod("alpha");  // a = 0x61
        CreateMod("Beta");   // B = 0x42
        CreateMod("gamma");  // g = 0x67

        var result = await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        // Ordinal: заглавные раньше строчных.
        // Было бы case-insensitive: alpha, Beta, gamma, Zeta.
        result.Select(m => m.Name).Should().Equal(
            "Beta", "Zeta", "alpha", "gamma");
    }

    // ------------------------------------------------------------------
    //  Файлы в mods/ игнорируются
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_FilesInMods_Ignored()
    {
        CreateMod("RealMod");
        File.WriteAllText(Path.Combine(_modsPath, "readme.txt"), "junk");
        File.WriteAllText(Path.Combine(_modsPath, "some.esp"), "junk");

        var result = await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("RealMod");
    }

    // ------------------------------------------------------------------
    //  Спецсимволы в именах
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_ModNamesWithSpacesAndSymbols_KeptVerbatim()
    {
        CreateMod("SkyUI - RU");
        CreateMod("[NoDelete] MyMod");
        CreateMod("# separator");

        var result = await _step.ExecuteAsync(
            _instancePath, CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(m => m.Name).Should().Contain(
            new[] { "# separator", "[NoDelete] MyMod", "SkyUI - RU" });
    }

    // ------------------------------------------------------------------
    //  Отмена
    // ------------------------------------------------------------------

    [Fact]
    public async Task Execute_CanceledToken_Throws()
    {
        CreateMod("SkyUI");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _step.ExecuteAsync(
            _instancePath, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
