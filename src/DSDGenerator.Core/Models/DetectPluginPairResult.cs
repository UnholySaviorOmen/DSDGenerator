// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Результат DetectPluginPairStep.
///
/// Pairs — пары плагинов, готовые к прогону через esp2dsd.
/// Skipped — причины, по которым какие-то плагины не попали в Pairs.
///
/// Порядок: Pairs и Skipped идут в порядке ModPair (из FindRuPairStep),
/// внутри мода — по имени плагина (Ordinal).
/// </summary>
public sealed record DetectPluginPairResult
{
    public required IReadOnlyList<PluginPair> Pairs { get; init; }
    public required IReadOnlyList<SkippedPlugin> Skipped { get; init; }
}
