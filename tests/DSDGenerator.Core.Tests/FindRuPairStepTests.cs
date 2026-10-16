// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;
using DSDGenerator.Core.Steps;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DSDGenerator.Core.Tests;

public class FindRuPairStepTests
{
    private readonly FindRuPairStep _step =
        new(NullLogger<FindRuPairStep>.Instance);

    // ------------------------------------------------------------------
    //  Хелперы
    // ------------------------------------------------------------------

    private static ModFolder Mod(string name)
        => new()
        {
            Name = name,
            FullPath = $"/fake/mods/{name}",
        };

    private static ModFolder Mod(string name, string fullPath)
        => new()
        {
            Name = name,
            FullPath = fullPath,
        };

    private Task<FindRuPairResult> Run(params ModFolder[] mods)
        => _step.ExecuteAsync(mods, CancellationToken.None);

    // ------------------------------------------------------------------
    //  Базовые сценарии
    // ------------------------------------------------------------------

    [Fact]
    public async Task Empty_ReturnsEmpty()
    {
        var result = await Run();

        result.Pairs.Should().BeEmpty();
        result.OrphanOriginals.Should().BeEmpty();
        result.OrphanRuMods.Should().BeEmpty();
    }

    [Fact]
    public async Task OriginalWithoutRu_GoesToOrphanOriginals()
    {
        var result = await Run(Mod("SkyUI"));

        result.Pairs.Should().BeEmpty();
        result.OrphanOriginals.Should().HaveCount(1);
        result.OrphanOriginals[0].Name.Should().Be("SkyUI");
        result.OrphanRuMods.Should().BeEmpty();
    }

    [Fact]
    public async Task RuWithoutOriginal_GoesToOrphanRuMods()
    {
        var result = await Run(Mod("SkyUI - RU"));

        result.Pairs.Should().BeEmpty();
        result.OrphanOriginals.Should().BeEmpty();
        result.OrphanRuMods.Should().HaveCount(1);
        result.OrphanRuMods[0].Name.Should().Be("SkyUI - RU");
    }

    [Fact]
    public async Task SimplePair_Found()
    {
        var result = await Run(
            Mod("SkyUI", "/mods/SkyUI"),
            Mod("SkyUI - RU", "/mods/SkyUI - RU"));

        result.Pairs.Should().HaveCount(1);
        var pair = result.Pairs[0];
        pair.OriginalName.Should().Be("SkyUI");
        pair.RuName.Should().Be("SkyUI - RU");
        pair.OriginalPath.Should().Be("/mods/SkyUI");
        pair.RuPath.Should().Be("/mods/SkyUI - RU");

        result.OrphanOriginals.Should().BeEmpty();
        result.OrphanRuMods.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiplePairs_AllFound()
    {
        var result = await Run(
            Mod("A"),
            Mod("A - RU"),
            Mod("B"),
            Mod("B - RU"),
            Mod("C"));

        result.Pairs.Should().HaveCount(2);
        result.Pairs.Select(p => p.OriginalName).Should().Equal("A", "B");
        result.OrphanOriginals.Should().HaveCount(1);
        result.OrphanOriginals[0].Name.Should().Be("C");
        result.OrphanRuMods.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    //  Порядок
    // ------------------------------------------------------------------

    [Fact]
    public async Task PairsInOriginalOrder_AsInput()
    {
        // Input уже отсортирован в ScanModsStep. FindRuPairStep
        // сохраняет порядок оригиналов.
        var result = await Run(
            Mod("A"),
            Mod("A - RU"),
            Mod("B"),
            Mod("B - RU"));

        result.Pairs.Select(p => p.OriginalName).Should().Equal("A", "B");
    }

    [Fact]
    public async Task OrphansSortedOrdinal()
    {
        var result = await Run(
            Mod("Zeta"),
            Mod("alpha"),
            Mod("Beta"));

        result.OrphanOriginals.Select(m => m.Name).Should().Equal(
            "Beta", "Zeta", "alpha");
    }

    // ------------------------------------------------------------------
    //  Спецсимволы в именах
    // ------------------------------------------------------------------

    [Fact]
    public async Task NamesWithSpacesAndSymbols()
    {
        var result = await Run(
            Mod("SkyUI - RU"),  // RU
            Mod("Some Mod [Patch]"),
            Mod("Some Mod [Patch] - RU"));

        result.Pairs.Should().HaveCount(1);
        result.Pairs[0].OriginalName.Should().Be("Some Mod [Patch]");
        result.Pairs[0].RuName.Should().Be("Some Mod [Patch] - RU");
        result.OrphanRuMods.Should().HaveCount(1);
        result.OrphanRuMods[0].Name.Should().Be("SkyUI - RU");
    }

    // ------------------------------------------------------------------
    //  Пограничные случаи
    // ------------------------------------------------------------------

    [Fact]
    public async Task RuOfRu_GoesToOrphanRu()
    {
        // "Y - RU - RU" заканчивается на " - RU" → это RU-мод.
        // Его пара — "Y - RU", но "Y - RU" тоже RU. Не пара.
        var result = await Run(
            Mod("Y"),
            Mod("Y - RU"),
            Mod("Y - RU - RU"));

        result.Pairs.Should().HaveCount(1);
        result.Pairs[0].RuName.Should().Be("Y - RU");

        result.OrphanRuMods.Should().HaveCount(1);
        result.OrphanRuMods[0].Name.Should().Be("Y - RU - RU");
    }

    [Fact]
    public async Task OnlyRuSuffixWithNoOriginal()
    {
        // Мод " - RU" (голый суффикс) — теоретический случай.
        var result = await Run(Mod(" - RU"));

        result.Pairs.Should().BeEmpty();
        result.OrphanOriginals.Should().BeEmpty();
        result.OrphanRuMods.Should().HaveCount(1);
        result.OrphanRuMods[0].Name.Should().Be(" - RU");
    }

    [Fact]
    public async Task CaseInsensitiveSuffix()
    {
        // "SkyUI - ru" (lowercase суффикс) тоже распознаётся как RU.
        // Windows ФС регистронезависима, поэтому такое возможно.
        var result = await Run(
            Mod("SkyUI", "/mods/SkyUI"),
            Mod("SkyUI - ru", "/mods/SkyUI - ru"));

        result.Pairs.Should().HaveCount(1);
        result.Pairs[0].OriginalName.Should().Be("SkyUI");
        result.Pairs[0].RuName.Should().Be("SkyUI - ru");
    }

    [Fact]
    public async Task MixedCaseSuffix_MatchWorks()
    {
        // Если оригинал "SkyUI", а пара физически "SkyUI - Ru",
        // всё равно должно сматчиться (OrdinalIgnoreCase).
        var result = await Run(
            Mod("SkyUI", "/mods/SkyUI"),
            Mod("SkyUI - Ru", "/mods/SkyUI - Ru"));

        result.Pairs.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------
    //  Отмена
    // ------------------------------------------------------------------

    [Fact]
    public async Task CanceledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _step.ExecuteAsync(
            new[] { Mod("SkyUI") }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------
    //  Валидация аргументов
    // ------------------------------------------------------------------

    [Fact]
    public async Task NullMods_Throws()
    {
        var act = async () => await _step.ExecuteAsync(
            null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
