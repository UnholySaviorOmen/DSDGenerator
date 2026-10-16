// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

namespace DSDGenerator.Core.Models;

/// <summary>
/// Результат FindRuPairStep.
///
/// Pairs — найденные пары ModName / ModName - RU (в порядке оригинала).
/// OrphanOriginals — оригиналы без перевода.
/// OrphanRuMods — RU-моды без оригинала.
///
/// Оба orphan-списка отсортированы по имени (Ordinal) — детерминизм.
/// </summary>
public sealed record FindRuPairResult
{
    public required IReadOnlyList<ModPair> Pairs { get; init; }
    public required IReadOnlyList<ModFolder> OrphanOriginals { get; init; }
    public required IReadOnlyList<ModFolder> OrphanRuMods { get; init; }
}
