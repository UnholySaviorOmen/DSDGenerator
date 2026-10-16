// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Плоская сводка прогона. Только примитивы + список результатов
/// по каждому плагину (для отчёта).
///
/// Заполняется DsdPipeline в конце. CLI и любые потребители
/// отображают эту модель.
/// </summary>
public sealed record DsdSummary
{
    /// <summary>Путь к инстансу.</summary>
    public required string InstancePath { get; init; }

    /// <summary>Путь к выходной папке __DSD_Output/.</summary>
    public required string OutputPath { get; init; }

    /// <summary>Сколько папок модов в mods/.</summary>
    public required int ModsScanned { get; init; }

    /// <summary>Сколько пар ModName / ModName - RU найдено.</summary>
    public required int ModPairsFound { get; init; }

    /// <summary>Сколько оригиналов без перевода.</summary>
    public required int OrphanOriginals { get; init; }

    /// <summary>Сколько RU-модов без пары.</summary>
    public required int OrphanRuMods { get; init; }

    /// <summary>Сколько плагин-пар найдено во всех модах.</summary>
    public required int PluginPairsFound { get; init; }

    /// <summary>Сколько DSD-файлов сгенерировано (Kind == Generated).</summary>
    public required int Generated { get; init; }

    /// <summary>Сколько плагинов skipped (Kind == Skipped).</summary>
    public required int Skipped { get; init; }

    /// <summary>Сколько плагинов failed (Kind == Failed).</summary>
    public required int Failed { get; init; }

    /// <summary>Результаты по каждому плагину (в порядке обработки).</summary>
    public required IReadOnlyList<DsdResult> Results { get; init; }
}
