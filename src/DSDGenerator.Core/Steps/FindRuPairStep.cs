// SPDX-FileCopyrightText: 2026 UnholySaviorOmen
// SPDX-License-Identifier: GPL-3.0-only

using DSDGenerator.Core.Models;
using Microsoft.Extensions.Logging;

namespace DSDGenerator.Core.Steps;

/// <summary>
/// Находит пары ModName / ModName - RU среди папок модов.
///
/// Алгоритм:
///   1. Строим индекс lowercasedName → ModFolder.
///      (Windows ФС регистронезависима, поэтому "SkyUI - RU"
///      и "skyui - ru" — это одна папка. Индекс по lower-case
///      делает поиск корректным.)
///   2. Для каждого мода, чьё имя НЕ заканчивается на " - RU":
///      ищем "Name - RU" в индексе.
///        - нашли → ModPair, помечаем обе папки использованными.
///        - не нашли → orphan original.
///   3. Оставшиеся неиспользованные моды, чьё имя заканчивается
///      на " - RU" → orphan RU.
///
/// Пограничные случаи:
///   - "Y - RU - RU": заканчивается на " - RU" → это RU, а не
///     оригинал. Пара для него — "Y - RU", но "Y - RU" тоже
///     заканчивается на " - RU" → не оригинал. Итог: "Y - RU - RU"
///     окажется orphan RU. Двойные переводы не поддерживаются.
///   - Мод с именем ровно " - RU" теоретически возможен, но
///     на практике не встречается. Обрабатывается как RU-мод,
///     никого не найдёт и попадёт в orphanRuMods.
///
/// Ничего не пишет, ничего не удаляет.
/// </summary>
public sealed class FindRuPairStep
{
    private readonly ILogger<FindRuPairStep> _logger;

    public FindRuPairStep(ILogger<FindRuPairStep> logger)
    {
        _logger = logger;
    }

    public Task<FindRuPairResult> ExecuteAsync(
        IReadOnlyList<ModFolder> mods,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (mods is null)
            throw new ArgumentNullException(nameof(mods));

        _logger.LogInformation(
            "FindRuPair: matching {Count} mod(s)", mods.Count);

        // Индекс: lowercasedName → ModFolder.
        var byLower = new Dictionary<string, ModFolder>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
            byLower[mod.Name] = mod;

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var pairs = new List<ModPair>();
        var orphanOriginals = new List<ModFolder>();

        // Проход 1: найти пары.
        foreach (var mod in mods)
        {
            ct.ThrowIfCancellationRequested();

            if (IsRuMod(mod.Name))
                continue;

            var expectedRuName = mod.Name + DsdPaths.RuModSuffix;

            if (byLower.TryGetValue(expectedRuName, out var ruMod))
            {
                used.Add(mod.Name);
                used.Add(ruMod.Name);

                pairs.Add(new ModPair
                {
                    OriginalName = mod.Name,
                    RuName = ruMod.Name,
                    OriginalPath = mod.FullPath,
                    RuPath = ruMod.FullPath,
                });
            }
            else
            {
                orphanOriginals.Add(mod);
            }
        }

        // Проход 2: orphan RU-моды — все, что не попали в пары
        // и заканчиваются на " - RU".
        var orphanRuMods = new List<ModFolder>();

        foreach (var mod in mods)
        {
            ct.ThrowIfCancellationRequested();

            if (used.Contains(mod.Name))
                continue;

            if (!IsRuMod(mod.Name))
                continue;

            orphanRuMods.Add(mod);
        }

        // Сортировка — для детерминизма отчёта.
        orphanOriginals.Sort(static (a, b) =>
            string.CompareOrdinal(a.Name, b.Name));
        orphanRuMods.Sort(static (a, b) =>
            string.CompareOrdinal(a.Name, b.Name));

        _logger.LogInformation(
            "FindRuPair complete: {Pairs} pair(s), " +
            "{OrphanOriginal} original(s) without translation, " +
            "{OrphanRu} RU-mod(s) without original",
            pairs.Count, orphanOriginals.Count, orphanRuMods.Count);

        return Task.FromResult(new FindRuPairResult
        {
            Pairs = pairs,
            OrphanOriginals = orphanOriginals,
            OrphanRuMods = orphanRuMods,
        });
    }

    private static bool IsRuMod(string name)
        => name.EndsWith(
            DsdPaths.RuModSuffix, StringComparison.OrdinalIgnoreCase);
}
